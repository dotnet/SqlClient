// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { test } = require('node:test');
const { validateTriageOutput } = require('../validate-triage-output.cjs');

const summary = `## \u{1F50D} Triage Summary

| Check | Result |
|-------|--------|
| Issue type | Bug |
| Environment | Missing: SQL Server version |
| Area | Area\\Async |
| Duplicates | None found |
| Regression | Not indicated |

### Analysis

Cancellation leaves the operation running. Investigate the async cancellation path; P1.

### Next Steps

- Ask the author for their SQL Server version.
`;

const output = (body) => ({ items: [{ type: 'add_comment', body }], errors: [] });

test('accepts initial, follow-up, and on-demand summaries with multiline content', () => {
    for (const suffix of ['', ' (updated after author response)', ' (on-demand re-triage)']) {
        validateTriageOutput(output(summary.replace('Triage Summary', `Triage Summary${suffix}`)));
    }
    validateTriageOutput(output(summary.replaceAll('\n', '\r\n')));
});

test('accepts a summary followed by an allowed label operation', () => {
    const value = output(summary);
    value.items.push({ type: 'add_labels', labels: ['Auto-Triage: Waiting for Author'] });
    validateTriageOutput(value);
});

test('preserves explicit no-op and failure reporting without a comment', () => {
    for (const type of ['noop', 'report_incomplete', 'missing_tool', 'missing_data']) {
        validateTriageOutput({ items: [{ type, reason: 'Required context unavailable' }] });
    }
});

test('rejects the placeholder bodies observed in failed runs, even with attribution', () => {
    for (const body of ['-', '@-', 'test message please ignore', 'test simple body', '']) {
        assert.throws(() => validateTriageOutput(output(
            `${body}\n\n<!-- gh-aw-workflow-call-id: dotnet/SqlClient/issue-triage -->`
        )), /Triage Summary/);
    }
});

test('rejects quoted or fenced copies of the summary', () => {
    for (const body of [
        summary.split('\n').map(line => `> ${line}`).join('\n'),
        `\`\`\`markdown\n${summary}\n\`\`\``,
    ]) {
        assert.throws(() => validateTriageOutput(output(body)), /Triage Summary/);
    }
});

test('requires every check row and non-placeholder values', () => {
    for (const field of ['Issue type', 'Environment', 'Area', 'Duplicates', 'Regression']) {
        const row = summary.split('\n').find(line => line.startsWith(`| ${field} |`));
        for (const replacement of ['', `| ${field} | |`, `| ${field} | <fill in> |`, `| ${field} | **TODO** |`]) {
            assert.throws(() => validateTriageOutput(output(summary.replace(row, replacement))), /check row/);
        }
    }
});

test('requires populated Analysis and Next Steps sections', () => {
    assert.throws(() => validateTriageOutput(output(summary.replace(
        'Cancellation leaves the operation running. Investigate the async cancellation path; P1.', ''
    ))), /Analysis/);
    assert.throws(() => validateTriageOutput(output(summary.replace(
        '- Ask the author for their SQL Server version.', ''
    ))), /Next Steps/);
    assert.throws(() => validateTriageOutput(output(summary.replace(
        '### Analysis', '### Explanation'
    ))), /Analysis/);
    assert.throws(() => validateTriageOutput(output(summary.replace(
        '- Ask the author for their SQL Server version.',
        '<!-- gh-aw-workflow-call-id: dotnet/SqlClient/issue-triage -->\n> AI-generated summary.'
    ))), /Next Steps/);
});

test('fenced examples cannot supply missing check rows or sections', () => {
    const body = summary.replace(
        '| Check | Result |', '```markdown\n| Check | Result |'
    ).replace('### Next Steps', '```\n### Next Steps');
    assert.throws(() => validateTriageOutput(output(body)), /check row/);
});

test('rejects multiple comments and mixed success/incomplete outcomes', () => {
    assert.throws(() => validateTriageOutput({
        items: [output(summary).items[0], output(summary).items[0]],
    }), /one comment/);
    assert.throws(() => validateTriageOutput({
        items: [output(summary).items[0], { type: 'report_incomplete', reason: 'Quota exceeded' }],
    }), /incomplete/);
    assert.throws(() => validateTriageOutput({
        items: [output(summary).items[0], { type: 'noop', reason: 'No action needed' }],
    }), /no-op/);
});

test('rejects missing output, empty results, and label-only results', () => {
    for (const value of [null, {}, { items: null }, { items: [] }, { items: [null] }]) {
        assert.throws(() => validateTriageOutput(value), /output|result/);
    }
    assert.throws(() => validateTriageOutput({
        items: [{ type: 'add_labels', labels: ['Auto-Triage: Waiting for Author'] }],
    }), /summary/);
});

test('the documented jq commands preserve the complete Markdown payload', () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'sqlclient-triage-'));
    const markdown = path.join(directory, 'triage-summary.md');
    const payload = path.join(directory, 'triage-summary.json');
    const body = summary +
        '\nPreserve "quotes", `backticks`, $HOME, $(not-a-command), and \\ paths.\n';
    const jq = process.env.JQ_PATH || 'jq';
    try {
        fs.writeFileSync(markdown, body, 'utf8');
        const encode = spawnSync(jq, ['-Rs', '{body: .}', markdown], { encoding: 'utf8' });
        assert.ifError(encode.error);
        assert.equal(encode.status, 0, encode.stderr);
        fs.writeFileSync(payload, encode.stdout, 'utf8');
        const check = spawnSync(jq, [
            '-e', 'type == "object" and (.body | type == "string" and length > 0)', payload,
        ], { encoding: 'utf8' });
        assert.ifError(check.error);
        assert.equal(check.status, 0, check.stderr);
        const result = JSON.parse(fs.readFileSync(payload, 'utf8'));
        assert.deepEqual(result, { body });
        validateTriageOutput(output(result.body));
    } finally {
        fs.rmSync(markdown, { force: true });
        fs.rmSync(payload, { force: true });
        fs.rmdirSync(directory);
    }
});
