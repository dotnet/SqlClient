// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const classify = require('../is-release-notes-only.cjs');

function fixture(files = [
    { filename: 'release-notes/6.1/6.1.8.md', status: 'added' },
    { filename: 'release-notes/6.1/README.md', status: 'modified' },
    { filename: 'CHANGELOG.md', status: 'modified' },
]) {
    const pr = {
        number: 4920,
        state: 'open',
        milestone: { id: 61, title: '6.1.8', state: 'open' },
        head: { sha: 'head-sha' },
        base: { ref: 'main', sha: 'base-sha' },
        changed_files: files.length,
    };
    const calls = [];
    const notices = [];
    const live = structuredClone(pr);
    const github = { rest: { pulls: {
        get: async (args) => {
            calls.push(['get', args]);
            return { data: structuredClone(live) };
        },
        listFiles: async (args) => {
            calls.push(['listFiles', args]);
            return { data: files.slice((args.page - 1) * args.per_page, args.page * args.per_page) };
        },
    } } };
    const context = {
        repo: { owner: 'dotnet', repo: 'SqlClient' },
        payload: { pull_request: pr, repository: { default_branch: 'main' } },
    };
    return { github, context, core: { notice: message => notices.push(message) }, pr, live, calls, notices };
}

test('PR 4920 release notes bypass branch and version rules with an open servicing milestone', async () => {
    const f = fixture();
    assert.equal(await classify(f), true);
    assert.equal(f.notices.length, 1);
    assert.equal(f.calls.filter(([name]) => name === 'get').length, 2);
});

for (const filename of [
    'src/Driver.cs', '.github/workflows/check-milestone.yml', 'README.md',
    'CHANGELOG.md.old', 'docs/CHANGELOG.md', 'release-notes-other/file.md',
    'release-notes', 'release-notes/../src/Driver.cs', 'release-notes//file.md',
    'release-notes/./file.md', 'release-notes\\file.md',
]) {
    test(`mixed changes including ${filename} preserve existing rules`, async () => {
        const f = fixture([{ filename: 'release-notes/a.md', status: 'added' }, { filename, status: 'modified' }]);
        assert.equal(await classify(f), false);
        assert.equal(f.notices.length, 0);
    });
}

for (const status of ['added', 'modified', 'removed', 'changed', 'unchanged']) {
    test(`${status} release-note paths qualify`, async () => {
        assert.equal(await classify(fixture([{ filename: 'release-notes/a.md', status }])), true);
    });
}

for (const status of ['renamed', 'copied']) {
    test(`${status} files require both paths inside the allowlist`, async () => {
        for (const previous_filename of ['src/Driver.cs', 'README.md']) {
            assert.equal(await classify(fixture([
                { filename: 'release-notes/a.md', previous_filename, status },
            ])), false);
        }
        assert.equal(await classify(fixture([
            { filename: 'release-notes/a.md', previous_filename: 'release-notes/b.md', status },
        ])), true);
        await assert.rejects(classify(fixture([
            { filename: 'release-notes/a.md', status },
        ])), /previous filename/);
    });
}

test('moving notes to code and deleting code cannot bypass rules', async () => {
    assert.equal(await classify(fixture([
        { filename: 'src/Driver.cs', previous_filename: 'release-notes/a.md', status: 'renamed' },
    ])), false);
    assert.equal(await classify(fixture([
        { filename: 'release-notes/a.md', status: 'added' },
        { filename: 'src/Driver.cs', status: 'removed' },
    ])), false);
});

test('a previous path outside the allowlist never qualifies even with a modified status', async () => {
    assert.equal(await classify(fixture([
        { filename: 'release-notes/a.md', previous_filename: 'src/Driver.cs', status: 'modified' },
    ])), false);
});

test('empty change sets do not qualify', async () => {
    const f = fixture([]);
    assert.equal(await classify(f), false);
    assert.equal(f.calls.length, 0);
});

for (const target of ['release/6.1', 'dev/feature']) {
    test(`${target} targets preserve existing rules without querying files`, async () => {
        const f = fixture();
        f.pr.base.ref = target;
        assert.equal(await classify(f), false);
        assert.equal(f.calls.length, 0);
    });
}

test('the default branch is not hardcoded to main', async () => {
    const f = fixture();
    f.context.payload.repository.default_branch = f.pr.base.ref = f.live.base.ref = 'trunk';
    assert.equal(await classify(f), true);
});

for (const milestone of [null, { id: 61, title: '6.1.8', state: 'closed' }]) {
    test(`a ${milestone ? 'closed' : 'missing'} milestone cannot bypass the gate`, async () => {
        const f = fixture();
        f.pr.milestone = milestone;
        await assert.rejects(classify(f), /open milestone/);
        assert.equal(f.calls.length, 0);
    });
}

for (const count of [undefined, -1, 1.5, '3', 3001]) {
    test(`invalid or API-capped file count ${count} fails closed`, async () => {
        const f = fixture();
        f.pr.changed_files = count;
        await assert.rejects(classify(f), /file count/);
    });
}

test('reads all pages, including code beyond the first 100 files', async () => {
    const files = Array.from({ length: 101 }, (_, i) => ({ filename: `release-notes/${i}.md`, status: 'added' }));
    const f = fixture(files);
    assert.equal(await classify(f), true);
    assert.deepEqual(f.calls.filter(([name]) => name === 'listFiles').map(([, args]) => args), [
        { owner: 'dotnet', repo: 'SqlClient', pull_number: 4920, per_page: 100, page: 1 },
        { owner: 'dotnet', repo: 'SqlClient', pull_number: 4920, per_page: 100, page: 2 },
    ]);
    files[100] = { filename: 'src/Driver.cs', status: 'modified' };
    assert.equal(await classify(fixture(files)), false);
});

for (const count of [100, 3000]) {
    test(`complete ${count}-file sets at page and API boundaries qualify`, async () => {
        const f = fixture(Array.from({ length: count }, (_, i) => ({ filename: `release-notes/${i}.md`, status: 'added' })));
        assert.equal(await classify(f), true);
        assert.equal(f.calls.filter(([name]) => name === 'listFiles').length, count / 100);
    });
}

for (const data of [[], {}, null, [{ filename: 'release-notes/a.md', status: 'added' }]]) {
    test(`incomplete or malformed page ${JSON.stringify(data)} fails closed`, async () => {
        const f = fixture();
        f.github.rest.pulls.listFiles = async () => ({ data });
        await assert.rejects(classify(f), /Incomplete/);
    });
}

test('a truncated later page fails closed', async () => {
    const f = fixture(Array.from({ length: 101 }, (_, i) => ({ filename: `release-notes/${i}.md`, status: 'added' })));
    const listFiles = f.github.rest.pulls.listFiles;
    f.github.rest.pulls.listFiles = args => args.page === 2 ? { data: [] } : listFiles(args);
    await assert.rejects(classify(f), /Incomplete/);
});

for (const method of ['get', 'listFiles']) {
    test(`${method} API errors fail the check rather than granting an exception`, async () => {
        const f = fixture();
        f.github.rest.pulls[method] = async () => { throw new Error('API unavailable'); };
        await assert.rejects(classify(f), /API unavailable/);
    });
}

test('later-page API errors are not swallowed', async () => {
    const f = fixture(Array.from({ length: 101 }, (_, i) => ({ filename: `release-notes/${i}.md`, status: 'added' })));
    const listFiles = f.github.rest.pulls.listFiles;
    f.github.rest.pulls.listFiles = async args => {
        if (args.page === 2) throw new Error('page 2 unavailable');
        return listFiles(args);
    };
    await assert.rejects(classify(f), /page 2 unavailable/);
});

for (const file of [
    null, {}, { status: 'added' }, { filename: 'release-notes/a.md' },
    { filename: 'release-notes/a.md', status: 'unknown' },
]) {
    test(`malformed file entry ${JSON.stringify(file)} fails closed`, async () => {
        await assert.rejects(classify(fixture([file])), /Invalid/);
    });
}

test('duplicate file entries cannot stand in for omitted files', async () => {
    const file = { filename: 'release-notes/a.md', status: 'added' };
    await assert.rejects(classify(fixture([file, file])), /Duplicate/);
});

for (const change of [
    pr => { pr.head.sha = 'new-head'; },
    pr => { pr.base.ref = 'release/6.1'; },
    pr => { pr.changed_files++; },
    pr => { pr.milestone.state = 'closed'; },
    pr => { pr.milestone = null; },
    pr => { pr.milestone.title = '8.0.0-preview1'; },
]) {
    test(`stale events and concurrent PR changes fail closed: ${change}`, async () => {
        const stale = fixture();
        change(stale.live);
        await assert.rejects(classify(stale), /changed/);
        const concurrent = fixture();
        const listFiles = concurrent.github.rest.pulls.listFiles;
        concurrent.github.rest.pulls.listFiles = async args => {
            const result = await listFiles(args);
            change(concurrent.live);
            return result;
        };
        await assert.rejects(classify(concurrent), /changed/);
    });
}

for (const filename of ['release-notes/a.md', 'src/Driver.cs']) {
    test(`reconciliation permits main advancing before retrieval for ${filename}`, async () => {
        const f = fixture([{ filename, status: 'modified' }]);
        f.live.base.sha = 'advanced-main';
        assert.equal(await classify(f), filename.startsWith('release-notes/'));
        assert.equal(f.calls.filter(([name]) => name === 'get').length, 2);
    });
}

for (const initialBase of ['base-sha', 'advanced-main']) {
    test(`base advancement during retrieval fails closed starting at ${initialBase}`, async () => {
        const f = fixture();
        f.live.base.sha = initialBase;
        const listFiles = f.github.rest.pulls.listFiles;
        f.github.rest.pulls.listFiles = async args => {
            const result = await listFiles(args);
            f.live.base.sha = 'changed-during-retrieval';
            return result;
        };
        await assert.rejects(classify(f), /changed/);
        assert.equal(f.calls.filter(([name]) => name === 'listFiles').length, 1);
        assert.equal(f.calls.filter(([name]) => name === 'get').length, 2);
    });
}

for (const duringRetrieval of [false, true]) {
    test(`missing live base SHA fails closed ${duringRetrieval ? 'after' : 'before'} retrieval`, async () => {
        const f = fixture();
        if (duringRetrieval) {
            const listFiles = f.github.rest.pulls.listFiles;
            f.github.rest.pulls.listFiles = async args => {
                const result = await listFiles(args);
                delete f.live.base.sha;
                return result;
            };
        } else {
            delete f.live.base.sha;
        }
        await assert.rejects(classify(f), /Missing live base commit identity/);
    });
}

test('both existing check names and rule steps are retained with explicit true-only bypasses', () => {
    const workflow = readFileSync(`${__dirname}/../../workflows/check-milestone.yml`, 'utf8');
    assert.match(workflow, /name: Validate milestone/);
    assert.match(workflow, /name: Check milestone matches next SqlClient version/);
    assert.equal((workflow.match(/return await classify\(\{ github, context, core \}\);/g) || []).length, 2);
    assert.equal((workflow.match(/steps\.release-notes\.outputs\.result != 'true'/g) || []).length, 2);
    assert.equal((workflow.match(/pull-requests: read/g) || []).length, 2);
    assert.match(workflow, /if: github\.event\.pull_request\.milestone == null/);
    assert.match(workflow, /milestone\.state != 'open'/);
    assert.match(workflow, /check-milestone-branch\.sh/);
    assert.match(workflow, /check-milestone-version\.sh/);
});
