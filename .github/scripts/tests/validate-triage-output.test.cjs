// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const {
    validateTriageOutput,
    validateTriageOutputFile,
} = require('../validate-triage-output.cjs');

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

const output = (body) => ({
    items: [{ type: 'add_comment', body }],
    errors: [],
});

test('accepts initial, follow-up, and on-demand summaries with multiline content', () => {
    for (const suffix of ['', ' (updated after author response)', ' (on-demand re-triage)']) {
        validateTriageOutput(output(summary.replace('Triage Summary', `Triage Summary${suffix}`)));
    }
    validateTriageOutput(output(summary.replaceAll('\n', '\r\n')));
});

test('rejects label operations before or after a summary and without one', () => {
    // Arrange
    const comment = output(summary).items[0];
    for (const type of ['add_labels', 'remove_labels']) {
        const label = { type, labels: ['Any label'] };
        for (const items of [
            [label, comment],
            [comment, label],
            [label],
            [{ type: 'noop', reason: 'No new information' }, label],
            [{ type: 'report_incomplete', reason: 'Context unavailable' }, label],
        ]) {
            // Act and Assert
            assert.throws(() => validateTriageOutput({ items }), /cannot change issue labels/);
        }
    }
});

test('validates the queued batch without rewriting its file', () => {
    // Arrange
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'triage-output-'));
    const filename = path.join(directory, 'agent_output.json');
    try {
        const queued = JSON.stringify(output(summary), null, 2);
        fs.writeFileSync(filename, queued);

        // Act
        validateTriageOutputFile(filename);

        // Assert
        assert.equal(fs.readFileSync(filename, 'utf8'), queued);

        // A rejected batch leaves the queued file unchanged.
        const invalid = JSON.stringify({
            items: [{ type: 'add_comment', body: 'test simple body' }],
        });
        fs.writeFileSync(filename, invalid);
        assert.throws(() => validateTriageOutputFile(filename), /Triage Summary/);
        assert.equal(fs.readFileSync(filename, 'utf8'), invalid);
    } finally {
        // Cleanup
        fs.rmSync(directory, { recursive: true, force: true });
    }
});

test('command line validation accepts a comment-only batch without rewriting it', () => {
    // Arrange
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'triage-output-'));
    const filename = path.join(directory, 'agent_output.json');
    const queued = JSON.stringify(output(summary), null, 2);
    fs.writeFileSync(filename, queued);
    try {
        // Act
        const result = spawnSync(process.execPath,
            [path.join(__dirname, '..', 'validate-triage-output.cjs')],
            { env: { ...process.env, GH_AW_AGENT_OUTPUT: filename }, encoding: 'utf8' });

        // Assert
        assert.equal(result.status, 0, result.stderr);
        assert.match(result.stdout, /Triage output validated before safe-output publication/);
        assert.equal(fs.readFileSync(filename, 'utf8'), queued);
    } finally {
        // Cleanup
        fs.rmSync(directory, { recursive: true, force: true });
    }
});

test('the compiled workflow validates the batch before publishing without label tools', () => {
    // Arrange
    const workflow = fs.readFileSync(
        path.join(__dirname, '..', '..', 'workflows', 'issue-triage.lock.yml'), 'utf8');

    // Act
    const validate = workflow.indexOf('name: Validate triage output before publishing');
    const validationCall = workflow.indexOf('validateTriageOutputFile(filename)', validate);
    const publish = workflow.indexOf('name: Process Safe Outputs', validate);

    // Assert
    assert.ok(validate >= 0 && validationCall > validate && publish > validationCall);
    assert.doesNotMatch(workflow.slice(validate, publish), /validateTriageOutput\(JSON\.parse/);
    assert.doesNotMatch(workflow, /add_labels|remove_labels|Auto-Triage: Waiting for Author/);
});

test('preserves explicit no-op and failure reporting without a comment', () => {
    for (const type of ['noop', 'report_incomplete', 'missing_tool', 'missing_data']) {
        validateTriageOutput({ items: [{ type, reason: 'Required context unavailable' }] });
    }
});

test('accepts all environment states without labels for each triage scenario', () => {
    for (const suffix of ['', ' (updated after author response)', ' (on-demand re-triage)']) {
        for (const environment of [
            'Missing: SQL Server version',
            '\u26A0\uFE0F Missing: SQL Server version',
            '\u26A0\uFE0F Partial: received OS; still missing SQL Server version',
            'All required environment details provided',
        ]) {
            const body = summary.replace('Triage Summary', `Triage Summary${suffix}`)
                .replace('Missing: SQL Server version', environment);

            // Act and Assert
            assert.doesNotThrow(() => validateTriageOutput(output(body)));
        }
    }
});

test('rejects diagnostic placeholders inside required rows and sections', () => {
    for (const content of [
        'Bug',
        'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
        '- Ask the author for their SQL Server version.',
    ]) {
        assert.throws(() => validateTriageOutput(output(summary.replace(
            content, 'test simple body'
        ))), /unfinished/);
    }
});

test('rejects duplicate required rows and sections', () => {
    for (const field of ['Issue type', 'Environment', 'Area', 'Duplicates', 'Regression']) {
        const row = summary.split('\n').find(line => line.startsWith(`| ${field} |`));
        for (const duplicate of [row, `| ${field} | |`, `| ${field} | <list> |`]) {
            assert.throws(() => validateTriageOutput(output(summary.replace(row, `${row}\n${duplicate}`))), /duplicate/i);
            assert.throws(() => validateTriageOutput(output(
                summary + `\n### Additional Context\n\n${duplicate}\n`
            )), /duplicate/i);
        }
    }
    for (const heading of ['Analysis', 'Next Steps']) {
        assert.throws(() => validateTriageOutput(output(
            summary + `\n### ${heading}\n\nCompleted prose.\n`
        )), /duplicate/i);
    }
});

test('rejects unfinished content outside required fields without rejecting quoted examples', () => {
    for (const draft of ['Additional context: <missing details>', 'TODO: finish the report',
        '2-4 sentences: what the issue is about', 'Actionable items. Be specific, not vague.']) {
        assert.throws(() => validateTriageOutput(output(
            summary + `\n### Additional Context\n\n${draft}\n`
        )), /unfinished/i);
        validateTriageOutput(output(summary + `\n### Additional Context\n\n> ${draft}\n`));
        validateTriageOutput(output(summary + `\n### Additional Context\n\n\`\`\`text\n${draft}\n\`\`\`\n`));
    }
});

test('rejects the placeholder bodies observed in failed runs, even with attribution', () => {
    for (const body of ['-', '@-', 'test message please ignore', 'test simple body', '']) {
        assert.throws(() => validateTriageOutput(output(
            `${body}\n\n<!-- gh-aw-workflow-call-id: dotnet/SqlClient/issue-triage -->`
        )), /Triage Summary/);
    }
});

test('rejects indented headings even when the remaining summary is valid prose', () => {
    for (const indentation of ['    ', '\t', '\n    ', '\r\n\t']) {
        assert.throws(() => validateTriageOutput(output(indentation + summary)), /Triage Summary/);
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

test('accepts comparison operators in completed triage prose', () => {
    for (const text of [
        'Rows with value < 10 and value > 0 fail.',
        'Rows with value<10 and value>0 fail.',
        'Rows with value < upperBound and value > lowerBound fail.',
        'Rows with value <= 10 and value >= 0 fail.',
    ]) {
        for (const content of [
            'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
            '- Ask the author for their SQL Server version.',
        ]) {
            validateTriageOutput(output(summary.replace(content, text)));
        }
    }
});

test('accepts generic types and XML documentation in completed prose', () => {
    for (const text of [
        'The failing method returns Task<Result>.',
        'The reproduction uses List<Item>.',
        'The reproduction uses Task<List<Item>>.',
        'The metadata is stored in Dictionary<string, Result>.',
        'The XML documentation contains a <summary>description</summary> tag.',
        'The XML payload contains <items><result>1</result></items>.',
        'The component is wrapped in Task<Component>.',
    ]) {
        for (const content of [
            'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
            '- Ask the author for their SQL Server version.',
        ]) {
            validateTriageOutput(output(summary.replace(content, text)));
        }
    }
});

test('rejects known workflow template instructions embedded in prose', () => {
    for (const template of [
        '<Bug / Feature / Question / Task>',
        '<All required environment details provided for investigation>',
        '<Best matching area from classification table>',
        '<None found / Potentially related: #NNN, #NNN>',
        '<Not indicated / Likely regression from vX.Y.Z / Inconclusive>',
        '<Actionable items. Be specific, not vague.>',
    ]) {
        assert.throws(() => validateTriageOutput(output(summary.replace(
            'Cancellation leaves the operation running. Investigate the async cancellation path; P1.',
            `Observed failure. ${template}`
        ))), /Analysis/);
    }
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
    value.errors.push({ type: 'add_comment', message: 'Comment operation exceeded quota' });
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
        items: [{ type: 'add_labels', labels: ['Any label'] }],
    }), /cannot change issue labels/);
});
