// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const assert = require('node:assert/strict');
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
    for (const type of ['add_labels', 'remove_labels']) {
        const value = output(summary);
        value.items.push({ type, labels: ['Auto-Triage: Waiting for Author'] });
        validateTriageOutput(value);
    }
});

test('rejects label operations before the summary', () => {
    for (const type of ['add_labels', 'remove_labels']) {
        const value = output(summary);
        value.items.unshift({ type, labels: ['Auto-Triage: Waiting for Author'] });
        assert.throws(() => validateTriageOutput(value), /before/);
    }
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

test('rejects embedded template placeholders in rows and prose sections', () => {
    const unfinished = [
        [summary.replace('Missing: SQL Server version', 'Missing: <list>'), /check row/],
        [summary.replace('Missing: SQL Server version', 'Missing: <component>'), /check row/],
        [summary.replace(
            'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
            'Cancellation leaves the operation running. Investigate <the affected component>; P1.'
        ), /Analysis/],
        [summary.replace(
            '- Ask the author for their SQL Server version.',
            '- Ask the author for their <missing details>.'
        ), /Next Steps/],
    ];
    for (const [body, message] of unfinished) {
        assert.throws(() => validateTriageOutput(output(body)), message);
    }

    validateTriageOutput(output(summary.replace(
        'Cancellation leaves the operation running.',
        'Cancellation leaves the operation running; SqlDataReader.GetFieldValue<T>() is affected.'
    )));
    validateTriageOutput(output(summary.replace(
        'Cancellation leaves the operation running.',
        'Cancellation leaves the operation running; Dictionary<TKey, TValue> maps the metadata.'
    )));
    validateTriageOutput(output(summary.replace(
        'Missing: SQL Server version',
        'Missing: SQL Server version; Dictionary<string, object> holds the available fields'
    )));
    validateTriageOutput(output(summary.replace(
        'Cancellation leaves the operation running.',
        'Cancellation leaves the operation running; see <https://example.com/docs> for context.'
    )));
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

test('rejects multiline template placeholders embedded in completed-looking prose', () => {
    for (const placeholder of [
        '<2-4 sentences: what the issue is about, which component is likely affected,\nand severity assessment (P0-P3)>',
        '< missing\ndetails >',
    ]) {
        for (const [content, message] of [
            ['Cancellation leaves the operation running. Investigate the async cancellation path; P1.', /Analysis/],
            ['- Ask the author for their SQL Server version.', /Next Steps/],
        ]) {
            const body = summary.replace(content, `Observed failure. ${placeholder}`);
            assert.throws(() => validateTriageOutput(output(body)), message);
            assert.throws(() => validateTriageOutput(output(body.replaceAll('\n', '\r\n'))), message);
        }
    }
});

test('rejects marker-prefixed drafts in check rows and required sections', () => {
    for (const draft of [
        'TODO: replace this with the actual analysis.',
        'TBD: determine next steps.',
        '**todo** investigate cancellation.',
        '- TBD: determine next steps.',
        '1. TODO: request reproduction details.',
        'Placeholder: insert findings here.',
    ]) {
        for (const [content, message] of [
            ['Missing: SQL Server version', /check row/],
            ['Cancellation leaves the operation running. Investigate the async cancellation path; P1.', /Analysis/],
            ['- Ask the author for their SQL Server version.', /Next Steps/],
        ]) {
            assert.throws(() => validateTriageOutput(output(
                summary.replace(content, draft)
            )), message);
        }
    }
    assert.throws(() => validateTriageOutput(output(summary.replace(
        '- Ask the author for their SQL Server version.',
        '- Ask the author for their SQL Server version.\n- TODO: decide the next action.'
    ))), /Next Steps/);
});

test('accepts completed prose that mentions markers without using them as draft prefixes', () => {
    for (const text of [
        'The reproduction contains a TODO comment in application code.',
        'TodoList cancellation leaves the operation running.',
        'TODOs in the sample do not affect the reproduction.',
    ]) {
        validateTriageOutput(output(summary.replace(
            'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
            text
        )));
    }
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
    for (const type of ['report_incomplete', 'missing_tool', 'missing_data']) {
        const items = [{ type: 'noop', reason: 'No action needed' }, { type, reason: 'Context unavailable' }];
        assert.throws(() => validateTriageOutput({ items }), /no-op.*incomplete/);
        assert.throws(() => validateTriageOutput({ items: [...items].reverse() }), /no-op.*incomplete/);
    }
});

test('rejects output errors even when a complete summary is queued', () => {
    const value = output(summary);
    value.items.push({ type: 'add_labels', labels: ['Auto-Triage: Waiting for Author'] });
    value.errors.push({ type: 'remove_labels', message: 'Label operation exceeded quota' });
    assert.throws(() => validateTriageOutput(value), /errors/);
    assert.throws(() => validateTriageOutput({
        items: [{ type: 'noop', reason: 'No new information' }],
        errors: ['Malformed NDJSON line'],
    }), /errors/);
    assert.throws(() => validateTriageOutput({ ...output(summary), errors: null }), /errors/);
});

test('rejects missing output, empty results, and label-only results', () => {
    for (const value of [null, {}, { items: null }, { items: [] }, { items: [null] }]) {
        assert.throws(() => validateTriageOutput(value), /output|result/);
    }
    assert.throws(() => validateTriageOutput({
        items: [{ type: 'add_labels', labels: ['Auto-Triage: Waiting for Author'] }],
    }), /summary/);
});
