// Auto-assign PR load balancer.
//
// Selects up to 2 assignees for a qualifying PR from a configurable pool,
// balancing by current open-PR assignment count. Invoked by the
// `auto-assign-pr.yml` workflow via `actions/github-script`.
module.exports = async ({ github, context, core }) => {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const prNumber = context.issue.number;
  const author = context.payload.pull_request.user.login;
  const normalizeLogin = login => login.toLowerCase();
  const parseCsvLogins = value => (value ?? '')
    .split(',')
    .map(entry => entry.trim())
    .filter(entry => entry.length > 0);

  // Fallback pool keeps behavior unchanged when no repo variable is configured.
  const defaultPool = ['cheenamalhotra', 'paulmedynski', 'priyankatiwari08', 'benrr101', 'mdaigle', 'apoorvdeshmukh'];
  const configuredPool = parseCsvLogins(process.env.AUTO_ASSIGN_PR_POOL);
  const rawPool = configuredPool.length > 0 ? configuredPool : defaultPool;
  const skipUsers = new Set(parseCsvLogins(process.env.AUTO_ASSIGN_PR_SKIP).map(normalizeLogin));
  const seenPoolUsers = new Set();
  const pool = [];
  for (const user of rawPool) {
    const normalized = normalizeLogin(user);
    if (!seenPoolUsers.has(normalized)) {
      seenPoolUsers.add(normalized);
      pool.push(user);
    }
  }

  let latestPr;
  try {
    const response = await github.rest.pulls.get({
      owner,
      repo,
      pull_number: prNumber
    });
    latestPr = response.data;
  } catch (error) {
    throw new Error(`Failed to fetch latest PR details: ${error.message}`);
  }

  if (latestPr.draft || !latestPr.milestone) {
    console.log('PR is no longer assignment-eligible (draft or missing milestone).');
    return;
  }

  const currentAssignees = (latestPr.assignees ?? []).map(a => a.login);

  console.log(`PR Author: ${author}`);
  console.log(`Event Name: ${context.eventName}; Is Fork PR: ${context.payload.pull_request.head.repo.fork === true}`);
  console.log(`Current Assignees: ${currentAssignees.join(', ')}`);

  if (currentAssignees.length >= 2) {
    console.log('PR already has 2 or more assignees. No action needed.');
    return;
  }

  const neededAssigneesCount = 2 - currentAssignees.length;

  const candidates = pool.filter(user =>
    normalizeLogin(user) !== normalizeLogin(author) &&
    !skipUsers.has(normalizeLogin(user)) &&
    !currentAssignees.some(a => normalizeLogin(a) === normalizeLogin(user))
  );

  if (candidates.length === 0) {
    console.log('No valid candidates left in the pool.');
    return;
  }

  const workloads = {};
  const canonicalCandidateByNormalized = {};
  candidates.forEach(user => {
    const normalized = normalizeLogin(user);
    workloads[normalized] = 0;
    canonicalCandidateByNormalized[normalized] = user;
  });

  try {
    // Rank candidates by current assignment count across all open PRs.
    const iterator = github.paginate.iterator(github.rest.pulls.list, {
      owner,
      repo,
      state: 'open',
      per_page: 100
    });

    for await (const response of iterator) {
      for (const pr of response.data) {
        if (pr.draft) continue;
        if (pr.assignees) {
          for (const assignee of pr.assignees) {
            const login = normalizeLogin(assignee.login);
            if (workloads[login] !== undefined) {
              workloads[login]++;
            }
          }
        }
      }
    }
  } catch (error) {
    throw new Error(`Failed to fetch open PRs for auto-assignment: ${error.message}`);
  }

  const workloadArray = candidates.map(user => {
    const normalized = normalizeLogin(user);
    return { user: canonicalCandidateByNormalized[normalized], count: workloads[normalized] };
  });
  console.log('Current Workloads:', workloadArray);

  // Shuffle before sorting so ties are broken fairly instead of favoring pool order.
  for (let i = workloadArray.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [workloadArray[i], workloadArray[j]] = [workloadArray[j], workloadArray[i]];
  }

  workloadArray.sort((a, b) => a.count - b.count);

  const selectedAssignees = workloadArray.slice(0, neededAssigneesCount).map(w => w.user);
  console.log(`Selected candidates: ${selectedAssignees.join(', ')}`);

  if (selectedAssignees.length === 0) {
    console.log('No assignees selected. No action needed.');
    return;
  }

  await github.rest.issues.addAssignees({
    owner,
    repo,
    issue_number: prNumber,
    assignees: selectedAssignees
  });
};
