// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { runInNewContext } = require('node:vm');
const assignPr = require('../auto-assign-pr.js');

const workflow = readFileSync(
    path.join(__dirname, '../../workflows/auto-assign-pr.yml'), 'utf8');
const condition = workflow.match(/^    if: (.+)$/m)?.[1];
assert.ok(condition, 'Expected a single-line job eligibility condition');

for (const fork of [false, true]) {
    for (const scenario of [
        { name: 'eligible', expected: true },
        { name: 'outside upstream', repository: 'contributor/SqlClient', expected: false },
        { name: 'closed', state: 'closed', expected: false },
        { name: 'draft', draft: true, expected: false },
        { name: 'without milestone', milestone: null, expected: false },
    ]) {
        test(`${fork ? 'fork' : 'same-repository'} PR: ${scenario.name}`, () => {
            const github = {
                repository: scenario.repository ?? 'dotnet/SqlClient',
                event: {
                    pull_request: {
                        state: scenario.state ?? 'open',
                        draft: scenario.draft ?? false,
                        milestone: scenario.milestone === null ? null : { number: 1 },
                        head: { repo: { fork } },
                    },
                },
            };
            // This condition uses only comparisons and boolean operators shared with JavaScript.
            assert.equal(runInNewContext(condition, { github }, { timeout: 1000 }),
                scenario.expected);
        });
    }
}

test('workflow retains the target event and default trusted-base checkout', () => {
    assert.match(workflow, /\n  pull_request_target:\r?\n/);
    assert.match(workflow, /types: \[opened, reopened, ready_for_review, milestoned\]/);
    assert.doesNotMatch(workflow, /^\s+(?:ref|repository):/m);
    const steps = workflow.split('    steps:')[1];
    assert.ok(steps, 'Expected workflow steps');
    assert.doesNotMatch(steps, /github\.event\.pull_request\.head/);
    assert.match(workflow, /uses: actions\/checkout@v6/);
    assert.match(workflow, /persist-credentials: false/);
    assert.match(workflow, /require\('\.\/\.github\/scripts\/auto-assign-pr\.js'\)/);
});

for (const existing of [[], ['existing-maintainer'], ['existing-one', 'existing-two']]) {
    test(`fork PR with ${existing.length} assignees preserves assignments and fills only vacancies`,
        async (t) => {
            const previousPool = process.env.PR_REVIEWER_POOL;
            process.env.PR_REVIEWER_POOL = 'author,least-loaded,busy';
            t.after(() => {
                if (previousPool === undefined) {
                    delete process.env.PR_REVIEWER_POOL;
                } else {
                    process.env.PR_REVIEWER_POOL = previousPool;
                }
            });

            const calls = [];
            const pullRequest = {
                state: 'open',
                draft: false,
                milestone: { number: 1 },
                user: { login: 'author' },
                head: { repo: { fork: true } },
                assignees: existing.map(login => ({ login })),
            };
            const github = {
                rest: {
                    pulls: {
                        get: async args => {
                            assert.deepEqual(args, {
                                owner: 'dotnet', repo: 'SqlClient', pull_number: 42,
                            });
                            return { data: pullRequest };
                        },
                        list: () => {},
                    },
                    issues: { addAssignees: async args => calls.push(args) },
                },
                paginate: {
                    async *iterator() {
                        assert.ok(existing.length < 2, 'Full assignments must not query workloads');
                        yield { data: [{ draft: false, assignees: [{ login: 'busy' }] }] };
                    },
                },
            };

            await assignPr({
                github,
                context: {
                    repo: { owner: 'dotnet', repo: 'SqlClient' },
                    issue: { number: 42 },
                    eventName: 'pull_request_target',
                    payload: { pull_request: pullRequest },
                },
                core: {},
            });

            assert.deepEqual(calls, existing.length === 2 ? [] : [{
                owner: 'dotnet',
                repo: 'SqlClient',
                issue_number: 42,
                assignees: existing.length === 0 ? ['least-loaded', 'busy'] : ['least-loaded'],
            }]);
            assert.deepEqual(pullRequest.assignees.map(a => a.login), existing);
        });
}
