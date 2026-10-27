// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Shared by both milestone jobs. Only a complete, nonempty default-branch
// release-notes change may bypass branch and next-version rules.
module.exports = async ({ github, context, core }) => {
    const pr = context.payload.pull_request;
    if (pr?.milestone?.state !== 'open') {
        throw new Error('An assigned open milestone is required, including for release notes.');
    }

    const defaultBranch = context.payload.repository?.default_branch;
    if (!defaultBranch || !pr.base?.ref) {
        throw new Error('Missing default or target branch for release-notes classification.');
    }
    if (pr.base.ref !== defaultBranch) {
        return false;
    }

    // GitHub's list-files endpoint returns at most 3,000 files. Never infer
    // eligibility from a truncated response or an unavailable expected count.
    const count = pr.changed_files;
    if (!Number.isSafeInteger(count) || count < 0 || count > 3000) {
        throw new Error('Cannot verify the complete changed-file count (must be 0..3000).');
    }
    if (count === 0) {
        return false;
    }
    if (!pr.head?.sha || !pr.base.sha) {
        throw new Error('Missing PR commit identity for release-notes classification.');
    }

    const args = { ...context.repo, pull_number: pr.number };
    const snapshot = value => JSON.stringify([
        value?.head?.sha, value?.base?.ref, value?.changed_files,
        value?.milestone?.id, value?.milestone?.title, value?.milestone?.state,
    ]);
    const verifySnapshot = async expectedBaseSha => {
        const { data } = await github.rest.pulls.get(args);
        if (!data?.base?.sha) {
            throw new Error('Missing live base commit identity for release-notes classification.');
        }
        if (snapshot(data) !== snapshot(pr)
            || (expectedBaseSha !== undefined && data.base.sha !== expectedBaseSha)) {
            throw new Error('PR changed since this event or during file retrieval; trigger a fresh milestone check.');
        }
        return data.base.sha;
    };

    // Reconciliation replays an older event after main may have advanced.
    // Match PR identity to the event, but compare base SHA only between live
    // snapshots so unrelated merges do not prevent a stable retrieval.
    const liveBaseSha = await verifySnapshot();
    const filenames = new Set();
    const statuses = new Set(['added', 'modified', 'removed', 'renamed', 'copied', 'changed', 'unchanged']);
    const allowedPath = path => typeof path === 'string'
        && !path.includes('\\')
        && path.split('/').every(part => part !== '' && part !== '.' && part !== '..')
        && (path === 'CHANGELOG.md' || path.startsWith('release-notes/'));
    let notesOnly = true;
    for (let page = 1; page <= Math.ceil(count / 100); page++) {
        const { data } = await github.rest.pulls.listFiles({ ...args, per_page: 100, page });
        if (!Array.isArray(data) || data.length !== Math.min(100, count - (page - 1) * 100)) {
            throw new Error(`Incomplete changed-file data on page ${page}.`);
        }
        for (const file of data) {
            if (!file || typeof file.filename !== 'string' || !file.filename || !statuses.has(file.status)) {
                throw new Error('Invalid changed-file entry.');
            }
            if (filenames.has(file.filename)) {
                throw new Error('Duplicate changed-file entry; the complete file set cannot be verified.');
            }
            filenames.add(file.filename);
            if ((file.status === 'renamed' || file.status === 'copied') && !file.previous_filename) {
                throw new Error('Missing previous filename for a renamed or copied file.');
            }
            // Check both sides, including deletions and code moved into notes.
            if (!allowedPath(file.filename)
                || (file.previous_filename !== undefined && !allowedPath(file.previous_filename))) {
                notesOnly = false;
            }
        }
    }
    await verifySnapshot(liveBaseSha);
    if (notesOnly) {
        core.notice('Release-notes-only PR on the default branch: skipping milestone branch and next-version rules; the open-milestone requirement still applies.');
    }
    return notesOnly;
};
