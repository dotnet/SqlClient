// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Pre-publication gate for issue-triage safe outputs: reject incomplete summaries
// and partial batches before any queued comment or label change reaches GitHub.
const fs = require('node:fs');

const TEMPLATE_PLACEHOLDER_PATTERN = /<([^<>]+)>/g;
const TEMPLATE_FIELD_PATTERN =
    /^(?:list|component|(?:the\s+)?affected\s+component|missing\s+details)$/i;
const TEMPLATE_INSTRUCTION_PATTERN =
    /^(?:fill in\b|2-4 sentences:|Actionable items\.|Bug\s*\/|All required environment details\b|Best matching area\b|None found\s*\/|Not indicated\s*\/)/i;

function hasTemplatePlaceholder(text) {
    // Match known template forms, not arbitrary angle brackets used by comparisons or C# generics.
    return [...text.matchAll(TEMPLATE_PLACEHOLDER_PATTERN)].some((match) => {
        const value = match[1].trim();
        const followsTypeName = /[A-Za-z_][\w.]*$/.test(text.slice(0, match.index));
        return TEMPLATE_INSTRUCTION_PATTERN.test(value) ||
            (!followsTypeName && TEMPLATE_FIELD_PATTERN.test(value));
    });
}

function proseOnly(body) {
    // Quoted examples, code blocks, and hidden attribution cannot satisfy required summary fields.
    let fence;
    return body.replace(/<!--[\s\S]*?(?:-->|$)/g, '').split('\n').map(line => {
        const marker = line.match(/^ {0,3}(`{3,}|~{3,})/);
        if (fence) {
            if (marker && marker[1][0] === fence[0] &&
                marker[1].length >= fence.length && line.trim() === marker[1]) {
                fence = undefined;
            }
            return '';
        }
        if (marker) {
            fence = marker[1];
            return '';
        }
        return /^(?: {0,3}>| {4}|\t)/.test(line) ? '' : line;
    }).join('\n');
}

function meaningful(value) {
    const text = value.replace(/[*_`]/g, '').trim();
    return /[\p{L}\p{N}]/u.test(text) &&
        !/^<[\s\S]*>$/.test(text) &&
        !hasTemplatePlaceholder(text) &&
        !/^\s*(?:(?:[-+]|\d+[.)])\s+)?(?:todo|tbd|placeholder)\b/im.test(text) &&
        !/^(?:todo|tbd|test(?: message| simple body)?(?: please ignore)?|placeholder)$/i.test(text);
}

function validateTriageOutput(output) {
    if (!output || !Array.isArray(output.items) || output.items.length === 0 ||
        output.items.some(item => !item || typeof item.type !== 'string')) {
        throw new Error('Missing or malformed triage output: expected non-empty result items.');
    }
    if (output.errors !== undefined &&
        (!Array.isArray(output.errors) || output.errors.length > 0)) {
        throw new Error('Triage output contains errors; refusing to publish a partial safe-output batch.');
    }

    // Explicit no-op/failure reports may proceed, but cannot accompany a summary or label changes.
    const comments = output.items.filter(item => item.type === 'add_comment');
    const incomplete = output.items.some(item =>
        ['report_incomplete', 'missing_tool', 'missing_data'].includes(item.type));
    const noop = output.items.some(item => item.type === 'noop');
    if (comments.length > 1) {
        throw new Error('Triage must publish at most one comment.');
    }
    if (incomplete && noop) {
        throw new Error('Triage cannot combine no-op and incomplete results.');
    }
    if (comments.length === 0) {
        if (!incomplete && !noop) {
            throw new Error('Triage output has no summary or explicit no-op/incomplete result.');
        }
        if (output.items.some(item => ['add_labels', 'remove_labels'].includes(item.type))) {
            throw new Error('Triage cannot change labels without a validated summary.');
        }
        return;
    }
    if (incomplete || noop) {
        throw new Error('Triage cannot publish a summary alongside an incomplete or no-op result.');
    }
    const commentIndex = output.items.indexOf(comments[0]);
    if (output.items.slice(0, commentIndex).some(item =>
        ['add_labels', 'remove_labels'].includes(item.type))) {
        throw new Error('Triage cannot change labels before its summary comment.');
    }

    const body = comments[0].body;
    if (typeof body !== 'string' ||
        !/^## (?:\u{1F50D} )?Triage Summary(?: \(updated after author response\)| \(on-demand re-triage\))? *\r?\n/u.test(body)) {
        throw new Error('Comment must start with the completed Triage Summary heading.');
    }
    // Require the workflow's five check rows and both prose sections, not merely its heading.
    const prose = proseOnly(body.replace(/\r\n?/g, '\n').trim());
    const table = prose.split(/^### /m)[0];
    let environment;
    for (const field of ['Issue type', 'Environment', 'Area', 'Duplicates', 'Regression']) {
        const rows = table.split('\n').filter(line => line.startsWith(`| ${field} |`));
        if (prose.split('\n').filter(line => line.startsWith(`| ${field} |`)).length > 1) {
            throw new Error(`Duplicate triage check row: ${field}.`);
        }
        const row = rows[0];
        const match = row?.match(/^\| [^|]+ \| (.*?) \| *$/);
        if (!match || !meaningful(match[1])) {
            throw new Error(`Missing or unfinished triage check row: ${field}.`);
        }
        if (field === 'Environment') {
            environment = match[1].replace(/[*_`]/g, '').trim();
        }
    }
    const sections = prose.split(/^### /m).slice(1);
    for (const heading of ['Analysis', 'Next Steps']) {
        const matchingSections = sections.filter(value => value.split('\n', 1)[0].trim() === heading);
        if (matchingSections.length > 1) {
            throw new Error(`Duplicate triage section: ${heading}.`);
        }
        const section = matchingSections[0];
        const content = section?.split('\n').slice(1).join('\n').split(/^#{1,3} /m)[0];
        if (!section || !meaningful(content)) {
            throw new Error(`Missing or unfinished triage section: ${heading}.`);
        }
    }
    // Extra prose must not reintroduce unfinished content outside the required fields.
    if (!meaningful(prose) ||
        /^(?:\s*(?:[-+]|\d+[.)])\s+)?\s*(?:test simple body|2-4 sentences:|Actionable items\.)/im.test(prose)) {
        throw new Error('Unfinished triage content outside the required fields.');
    }

    const labels = output.items.filter(item => ['add_labels', 'remove_labels'].includes(item.type));
    const onDemand = body.split('\n', 1)[0].includes('(on-demand re-triage)');
    if (onDemand) {
        if (labels.length > 0) {
            throw new Error('On-demand triage must preserve label state.');
        }
    } else {
        const missingEnvironment = /^(?:\u26A0\uFE0F?\s*)?(?:Missing|Partial):/iu.test(environment);
        const expectedType = missingEnvironment ? 'add_labels' : 'remove_labels';
        if (labels.length !== 1 || labels[0].type !== expectedType ||
            !Array.isArray(labels[0].labels) || labels[0].labels.length !== 1 ||
            labels[0].labels[0] !== 'Auto-Triage: Waiting for Author') {
            throw new Error(`Triage requires exactly one ${expectedType} operation for the Waiting for Author label.`);
        }
    }
}

if (require.main === module) {
    // gh-aw supplies the complete queued batch here; throwing blocks safe-output publication.
    const filename = process.env.GH_AW_AGENT_OUTPUT;
    if (!filename) {
        throw new Error('GH_AW_AGENT_OUTPUT is required; refusing to publish unvalidated triage output.');
    }
    validateTriageOutput(JSON.parse(fs.readFileSync(filename, 'utf8')));
    console.log('Triage output validated before safe-output publication.');
}

module.exports = { validateTriageOutput };
