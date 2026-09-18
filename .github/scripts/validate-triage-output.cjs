// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const fs = require('node:fs');

function proseOnly(body) {
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
        !/^(?:todo|tbd|test(?: message)?(?: please ignore)?|placeholder)$/i.test(text);
}

function validateTriageOutput(output) {
    if (!output || !Array.isArray(output.items) || output.items.length === 0 ||
        output.items.some(item => !item || typeof item.type !== 'string')) {
        throw new Error('Missing or malformed triage output: expected non-empty result items.');
    }

    const comments = output.items.filter(item => item.type === 'add_comment');
    const incomplete = output.items.some(item =>
        ['report_incomplete', 'missing_tool', 'missing_data'].includes(item.type));
    const noop = output.items.some(item => item.type === 'noop');
    if (comments.length > 1) {
        throw new Error('Triage must publish at most one comment.');
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

    const body = comments[0].body;
    if (typeof body !== 'string' ||
        !/^## (?:\u{1F50D} )?Triage Summary(?: \(updated after author response\)| \(on-demand re-triage\))? *\r?\n/u.test(body.trim())) {
        throw new Error('Comment must start with the completed Triage Summary heading.');
    }
    const prose = proseOnly(body.replace(/\r\n?/g, '\n').trim());
    const table = prose.split(/^### /m)[0];
    for (const field of ['Issue type', 'Environment', 'Area', 'Duplicates', 'Regression']) {
        const row = table.split('\n').find(line => line.startsWith(`| ${field} |`));
        const match = row?.match(/^\| [^|]+ \| (.*?) \| *$/);
        if (!match || !meaningful(match[1])) {
            throw new Error(`Missing or unfinished triage check row: ${field}.`);
        }
    }
    const sections = prose.split(/^### /m).slice(1);
    for (const heading of ['Analysis', 'Next Steps']) {
        const section = sections.find(value => value.split('\n', 1)[0].trim() === heading);
        const content = section?.split('\n').slice(1).join('\n').split(/^#{1,3} /m)[0];
        if (!section || !meaningful(content)) {
            throw new Error(`Missing or unfinished triage section: ${heading}.`);
        }
    }
}

if (require.main === module) {
    const filename = process.env.GH_AW_AGENT_OUTPUT;
    if (!filename) {
        throw new Error('GH_AW_AGENT_OUTPUT is required; refusing to publish unvalidated triage output.');
    }
    validateTriageOutput(JSON.parse(fs.readFileSync(filename, 'utf8')));
    console.log('Triage output validated before safe-output publication.');
}

module.exports = { validateTriageOutput };
