(() => {
  'use strict';
  const roots = [...document.querySelectorAll('[data-perf-tabs], [data-perf-compare], [data-perf-history], [data-perf-trends]')].filter(root => !root.dataset.perfReady);
  if (!roots.length) return;
  roots.forEach(root => { root.dataset.perfReady = 'true'; });
  const NS = 'http://www.w3.org/2000/svg';
  const DEFAULT_WINDOW = 30;
  const WINDOW_COUNTS = [10, 30, 60, 100];
  const SIGNIFICANT_PCT = 5;
  const names = { dotnet: '.NET', node: 'Node.js / Fable', 'node-jitless': 'Node.js (jitless)' };
  const number = value => Math.abs(value) < 10 && value !== 0
    ? value.toLocaleString('en-US', { maximumSignificantDigits: 3 })
    : value.toLocaleString('en-US', { maximumFractionDigits: 1 });
  const ratio = value => value > 0 && value < 0.1 ? value.toLocaleString('en-US', { maximumSignificantDigits: 2 }) : number(value);
  const percent = value => {
    const rounded = Math.round(value * 10) / 10;
    return `${rounded > 0 ? '+' : rounded < 0 ? '−' : '±'}${Math.abs(rounded).toFixed(1)}%`;
  };
  const short = commit => commit.Commit.slice(0, 7);
  const shortDate = commit => new Date(commit.Date).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' });
  const fullDate = commit => new Date(commit.Date).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
  const truncate = (text, limit = 52) => text.length > limit ? `${text.slice(0, limit - 1)}…` : text;
  const enginesIn = rows => [...new Set(rows.map(row => row.Engine))].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
  const rank = name => ['Ranvier', 'FSharp.Data.Adaptive', 'R3', 'Fable.Ripple'].findIndex(family => name.startsWith(family)) + 1 || 5;
  const family = engine => engine.split(' (')[0];
  const colour = engine => ['#3987e5', '#eb6834', '#1baf7a', '#bd8a31', '#9b7bc4'][rank(engine) - 1];
  // Selectable metrics keyed by identifier, with leg, label, unit and headings.
  const metrics = {
    instructions: { leg: 'arm64-counters', label: 'Instructions', unit: 'instr/op', title: 'Instructions', trends: 'Instruction trends' },
    cycles: { leg: 'arm64-counters', label: 'Cycles', unit: 'cycles/op', title: 'Cycles', trends: 'Cycle trends' },
    bytes: { leg: 'arm64-counters', label: 'Allocated bytes', unit: 'B/op', title: 'Allocations', trends: 'Allocation trends' },
    branch_misses: { leg: 'arm64-counters', label: 'Branch misses', unit: 'misses/op', title: 'Branch misses', trends: 'Branch-miss trends' },
    cpu_ns: { leg: 'x64-timing', label: 'CPU time (x64)', unit: 'ns/op', title: 'CPU time', trends: 'CPU-time trends' },
  };
  const measurements = (commit, runtime, metric = 'instructions') => commit.Medians.filter(row => row.Leg === metrics[metric].leg
    && row.Runtime === runtime && row.Metric === metric && (row.Side === undefined || row.Side === 'head'));
  // Medians below zero lie within measurement noise of zero and display as zero.
  const shown = value => Number.isFinite(value) ? Math.max(0, value) : null;
  const metricsFor = (commits, runtime) => Object.keys(metrics).filter(metric => commits.some(commit => measurements(commit, runtime, metric).length));
  const metricControl = (commits, runtime, current) => {
    const available = metricsFor(commits, runtime);
    return selectControl('Metric', available.map(key => [key, metrics[key].label]), available.includes(current) ? current : available[0]);
  };
  const processRange = row => Number.isFinite(row?.Min) && number(shown(row.Min)) !== number(shown(row.Max))
    ? `processes ${number(shown(row.Min))}–${number(shown(row.Max))}` : '';
  const countersFor = (commit, runtime, scenario, engine) => (commit.Counters || [])
    .filter(row => row.Runtime === runtime && row.Scenario === scenario && row.Engine === engine)
    .map(row => `${row.Name} ${number(row.Value)}`).join(', ');
  const ranvierVersion = commit => (commit.Packages || []).find(pkg => pkg.Name === 'Ranvier')?.Version || null;
  // Scoped acceptance entries matching the selected runtime, scenario, engine, leg and metric.
  const acceptedFor = (commit, runtime, scenario, engine, metric = 'instructions') => (commit?.Accepted || [])
    .filter(row => row.Runtime === runtime
      && row.Scenario === scenario
      && row.Engine === engine
      && (!row.Leg || row.Leg === metrics[metric].leg)
      && (!row.Metric || row.Metric === metric));

  const defaultFromChoice = commits => commits.length > DEFAULT_WINDOW ? `last:${DEFAULT_WINDOW}` : 'all';

  // Distinct Ranvier package version transitions ordered from newest to oldest.
  function versionTransitions(commits) {
    const seen = new Set();
    const list = [];
    commits.forEach((commit, index) => {
      const version = ranvierVersion(commit);
      if (!version || seen.has(version)) return;
      seen.add(version);
      list.push({ version, index, commit });
    });
    return list.reverse();
  }

  // Resolves a From selector value, count shorthand, version tag, or SHA prefix into a window anchor.
  function resolveFrom(commits, rawChoice) {
    const raw = typeof rawChoice === 'string' ? rawChoice.trim() : '';
    if (raw === 'all') return { choice: 'all', index: 0 };
    const lastMatch = /^last:([1-9][0-9]*)$/.exec(raw);
    const bareMatch = /^([1-9][0-9]{0,2})$/.exec(raw);
    const countText = lastMatch?.[1] || bareMatch?.[1];
    if (countText) {
      const count = Number(countText);
      if (count >= commits.length) return { choice: 'all', index: 0 };
      const index = commits.length - count;
      const choice = WINDOW_COUNTS.includes(count) ? `last:${count}` : commits[index].Commit;
      return { choice, index };
    }
    const verMatch = /^ver:(.+)$/.exec(raw);
    if (verMatch) {
      const index = commits.findIndex(commit => ranvierVersion(commit) === verMatch[1]);
      if (index >= 0) return { choice: `ver:${verMatch[1]}`, index };
    }
    if (/^[0-9a-f]{4,40}$/i.test(raw)) {
      const lower = raw.toLowerCase();
      const index = commits.findIndex(commit => commit.Commit.toLowerCase().startsWith(lower));
      if (index >= 0) return { choice: commits[index].Commit, index };
    }
    const choice = defaultFromChoice(commits);
    return { choice, index: choice === 'all' ? 0 : commits.length - DEFAULT_WINDOW };
  }

  const urlFromChoice = () => {
    try { return new URLSearchParams(location.search).get('from'); }
    catch { return null; }
  };

  // Synchronizes ?from= in the query string while preserving unrelated parameters and hash.
  function syncUrlFrom(choice, commits) {
    if (typeof history?.replaceState !== 'function') return;
    try {
      const url = new URL(location.href);
      if (choice === defaultFromChoice(commits)) url.searchParams.delete('from');
      else url.searchParams.set('from', /^[0-9a-f]{40}$/i.test(choice) ? choice.slice(0, 7) : choice);
      history.replaceState(null, '', url.toString());
    } catch { }
  }

  function element(tag, attributes = {}, text) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function svgElement(tag, attributes = {}, text) {
    const node = document.createElementNS(NS, tag);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function selectControl(label, options, value) {
    const wrapper = element('label', { class: 'rv-perf-control' });
    const select = element('select', { 'aria-label': label });
    for (const [key, title] of options) select.append(element('option', { value: key }, title));
    select.value = value;
    wrapper.append(element('span', {}, label), select);
    return { wrapper, select };
  }
  function fromControl(commits, rawChoice) {
    const resolved = resolveFrom(commits, rawChoice);
    const wrapper = element('label', { class: 'rv-perf-control rv-perf-control--from' });
    const select = element('select', { 'aria-label': 'From' });
    const windowGroup = element('optgroup', { label: 'Window' });
    for (const count of WINDOW_COUNTS) {
      if (commits.length <= count) continue;
      const anchor = commits[commits.length - count];
      windowGroup.append(element('option', { value: `last:${count}` }, `Last ${count} commits (${short(anchor)})`));
    }
    windowGroup.append(element('option', { value: 'all' }, `All ${commits.length} commits (${short(commits[0])})`));
    select.append(windowGroup);

    const versions = versionTransitions(commits);
    if (versions.length) {
      const versionGroup = element('optgroup', { label: 'Package versions' });
      for (const { version, commit } of versions)
        versionGroup.append(element('option', { value: `ver:${version}` }, `Since ${version} (${short(commit)} · ${shortDate(commit)})`));
      select.append(versionGroup);
    }

    const commitGroup = element('optgroup', { label: 'Commits (newest first)' });
    for (let i = commits.length - 1; i >= 0; i--) {
      const item = commits[i];
      commitGroup.append(element('option', { value: item.Commit }, `${short(item)} · ${shortDate(item)} · ${truncate(item.Subject)}`));
    }
    select.append(commitGroup);
    select.value = resolved.choice;
    wrapper.append(element('span', {}, 'From'), select);
    return { wrapper, select, resolved };
  }
  function makeSvg(label, height) {
    const svg = svgElement('svg', { viewBox: `0 0 640 ${height}`, role: 'group', 'aria-label': label });
    svg.append(svgElement('title', {}, label));
    return svg;
  }
  function engineToggle(engine, hidden, update) {
    const button = element('button', { type: 'button', class: 'rv-perf-engine', 'data-focus-key': JSON.stringify(['engine', engine]), 'aria-pressed': String(!hidden.has(engine)) });
    button.style.setProperty('--engine', colour(engine));
    button.append(element('i', { 'aria-hidden': 'true' }), document.createTextNode(engine));
    button.addEventListener('click', () => {
      hidden.has(engine) ? hidden.delete(engine) : hidden.add(engine);
      update();
    });
    return button;
  }
  function inspectable(node, message, output) {
    node.setAttribute('tabindex', '0');
    node.setAttribute('aria-label', message);
    node.append(svgElement('title', {}, message));
    node.addEventListener('pointerenter', () => { output.textContent = message; });
    node.addEventListener('pointerdown', () => { output.textContent = message; });
    node.addEventListener('focus', () => { output.textContent = message; });
  }
  function commitLink(commit) {
    return element('a', { href: `https://github.com/ranvier-org/Ranvier/commit/${commit.Commit}`, class: 'rv-perf-sha' }, short(commit));
  }
  function appendDeltaBadge(container, deltaPct, suffix = '') {
    const rounded = Math.round(deltaPct * 10) / 10;
    const cls = rounded <= -SIGNIFICANT_PCT ? 'rv-perf-delta is-improved'
      : rounded >= SIGNIFICANT_PCT ? 'rv-perf-delta is-regressed'
      : 'rv-perf-delta';
    const text = `${percent(deltaPct)}${suffix}`;
    const span = element('span', { class: cls });
    if (Math.abs(rounded) >= SIGNIFICANT_PCT) span.append(element('strong', {}, text));
    else span.textContent = text;
    container.append(span);
  }
  function comparison(scenario, rows, relative, output, baselineValue, metric = 'instructions', counters = () => '') {
    const panel = element('section', { class: 'rv-perf-panel' });
    const header = element('header');
    const sorted = [...rows].sort((a, b) => rank(a.Engine) - rank(b.Engine) || a.Engine.localeCompare(b.Engine));
    const baseline = shown(baselineValue ?? sorted.find(row => row.Engine === 'Ranvier')?.Value);
    const relativeAvailable = relative && baseline > 0;
    const unit = metrics[metric].unit;
    header.append(element('h4', {}, scenario), element('span', {}, relativeAvailable ? 'Ranvier = 1×' : unit));
    panel.append(header);
    if (relative && !relativeAvailable) panel.append(element('p', { class: 'rv-perf-note' }, 'Ranvier reference unavailable; showing absolute values.'));
    const max = Math.max(Number.MIN_VALUE, ...sorted.map(row => shown(row.Value) ?? 0));
    const svg = makeSvg(`${scenario}: ${metrics[metric].label.toLowerCase()} per operation by library`, 20 + sorted.length * 55);
    sorted.forEach((row, index) => {
      const y = 13 + index * 55;
      const group = svgElement('g', { class: 'rv-perf-bar', role: 'group' });
      const amount = shown(row.Value);
      const valid = amount !== null;
      const value = !valid ? 'unavailable' : relativeAvailable ? `${ratio(amount / baseline)}×` : number(amount);
      group.append(svgElement('text', { x: 0, y, class: 'rv-perf-svg-label' }, row.Engine));
      group.append(svgElement('text', { x: 640, y, 'text-anchor': 'end', class: 'rv-perf-svg-value' }, value));
      group.append(svgElement('line', { x1: 0, x2: 640, y1: y + 22, y2: y + 22, class: 'rv-perf-track' }));
      if (valid) group.append(svgElement('rect', { x: 0, y: y + 15, width: Math.max(amount === 0 ? 0 : 1.5, amount / max * 640), height: 14, rx: 2, fill: colour(row.Engine) }));
      group.append(svgElement('rect', { x: 0, y: y - 12, width: 640, height: 48, fill: 'transparent' }));
      const range = valid && processRange(row) ? ` (${processRange(row)})` : '';
      const library = counters(row.Engine);
      inspectable(group, `${scenario} · ${row.Engine}: ${valid ? `${number(amount)} ${unit}${range}` : 'measurement unavailable'}${relativeAvailable && valid ? ` · ${ratio(amount / baseline)}× Ranvier` : ''}${library ? ` · ${library}` : ''}`, output);
      svg.append(group);
    });
    panel.append(svg);
    return panel;
  }

  function mount(root, commits, shared) {
    const isHistory = root.hasAttribute('data-perf-history');
    const group = (root.dataset.perfGroup || '').split(',').filter(Boolean);
    const hidden = new Set();
    let selectedCommitSha = commits.at(-1).Commit;
    root.className = 'rv-perf';
    root.replaceChildren();
    const heading = element('div', { class: 'rv-perf-heading' });
    heading.append(element('p', { class: 'rv-perf-eyebrow' }, isHistory ? 'Across commits' : 'Library comparison'));
    const title = element('h3', {}, isHistory ? 'Instructions over time' : 'Instructions by library');
    heading.append(title);
    const controls = element('div', { class: 'rv-perf-controls' });
    const runtimes = [...new Set(commits.flatMap(commit => commit.Medians.filter(row => row.Leg === 'arm64-counters' && row.Metric === 'instructions').map(row => row.Runtime)))].sort();
    if (!runtimes.length) throw new Error('No instruction measurements are available yet.');
    const runtime = selectControl('Runtime', runtimes.map(key => [key, names[key] || key]), runtimes.includes(shared?.runtime) ? shared.runtime : runtimes.includes('dotnet') ? 'dotnet' : runtimes[0]);
    const from = isHistory ? fromControl(commits, shared?.from ?? urlFromChoice()) : null;
    const commit = selectControl('Commit', commits.map(item => [item.Commit, `${short(item)} · ${item.Subject}`]), selectedCommitSha);
    let metric = metricControl(commits, runtime.select.value, shared?.metric || 'instructions');
    const mode = isHistory
      ? selectControl('Values', [['change', 'Change since From'], ['absolute', 'Per operation'], ['relative', 'Relative to Ranvier']], 'absolute')
      : selectControl('Values', [['absolute', 'Per operation'], ['relative', 'Relative to Ranvier']], 'absolute');
    if (isHistory) controls.append(runtime.wrapper, metric.wrapper, from.wrapper, commit.wrapper, mode.wrapper);
    else controls.append(runtime.wrapper, metric.wrapper, commit.wrapper, mode.wrapper);
    const search = element('input', { type: 'search', placeholder: 'Find a scenario…', 'aria-label': 'Find a scenario', class: 'rv-perf-search' });
    const scenario = selectControl('Scenario', [], '');
    controls.append(isHistory ? scenario.wrapper : search);
    const legend = element('div', { class: 'rv-perf-legend', 'aria-label': 'Visible libraries' });
    const provenance = element('div', { class: 'rv-perf-provenance' });
    const body = element('div', { class: isHistory ? 'rv-perf-trend' : 'rv-perf-panels' });
    const detail = element('p', { class: 'rv-perf-inspect', role: 'status', 'aria-live': 'polite' }, 'Point to a bar or focus it to inspect the measurement.');
    root.append(heading, controls, legend, provenance, body, detail);
    let previousRuntime = runtime.select.value;
    let fromChangedByUser = false;

    function update() {
      const focusedKey = root.contains(document.activeElement) ? document.activeElement.getAttribute('data-focus-key') : null;
      if (previousRuntime !== runtime.select.value) {
        hidden.clear();
        const next = metricControl(commits, runtime.select.value, metric.select.value);
        metric.wrapper.replaceWith(next.wrapper);
        metric = next;
        metric.select.addEventListener('change', update);
      }
      previousRuntime = runtime.select.value;
      const measure = metric.select.value;
      title.textContent = `${metrics[measure].title} ${isHistory ? 'over time' : 'by library'}`;

      let windowCommits = commits;
      let fromChoice = 'all';
      if (isHistory) {
        const resolved = resolveFrom(commits, from.select.value);
        fromChoice = resolved.choice;
        from.select.value = fromChoice;
        windowCommits = commits.slice(resolved.index);
        commit.select.replaceChildren(...windowCommits.map(item => element('option', { value: item.Commit }, `${short(item)} · ${item.Subject}`)));
        if (!windowCommits.some(item => item.Commit === selectedCommitSha)) selectedCommitSha = windowCommits.at(-1).Commit;
        commit.select.value = selectedCommitSha;
      } else {
        selectedCommitSha = commit.select.value;
      }

      const selectedWindowIndex = Math.max(0, windowCommits.findIndex(item => item.Commit === selectedCommitSha));
      const selected = windowCommits[selectedWindowIndex] || commits.at(-1);
      const rows = measurements(selected, runtime.select.value, measure);
      const allRows = isHistory ? windowCommits.flatMap(item => measurements(item, runtime.select.value, measure)) : rows;
      const engines = enginesIn(allRows);
      const families = [...new Set(engines.map(family))];
      if (JSON.stringify(families) !== legend.dataset.families) {
        legend.replaceChildren(...families.map(engine => engineToggle(engine, hidden, update)));
        legend.dataset.families = JSON.stringify(families);
      }
      [...legend.children].forEach((button, index) => button.setAttribute('aria-pressed', String(!hidden.has(families[index]))));

      provenance.replaceChildren(commitLink(selected), document.createTextNode(` · ${fullDate(selected)} · ${selected.Subject}`));
      if (isHistory) {
        if (windowCommits.length < commits.length) provenance.append(document.createTextNode(` · Window: ${windowCommits.length} of ${commits.length} commits (from `), commitLink(windowCommits[0]), document.createTextNode(')'));
        if (selectedWindowIndex > 0) {
          const anchorButton = element('button', { type: 'button', class: 'rv-perf-action' }, `Start from ${short(selected)}`);
          anchorButton.addEventListener('click', () => {
            from.select.value = selected.Commit;
            fromChangedByUser = true;
            update();
          });
          provenance.append(document.createTextNode(' · '), anchorButton);
        }
        if (fromChoice !== defaultFromChoice(commits)) {
          const resetButton = element('button', { type: 'button', class: 'rv-perf-action' }, 'Reset window');
          resetButton.addEventListener('click', () => {
            from.select.value = defaultFromChoice(commits);
            fromChangedByUser = true;
            update();
          });
          provenance.append(document.createTextNode(' · '), resetButton);
        }
      }

      const available = [...new Set(allRows.map(row => row.Scenario))].filter(name => !group.length || group.some(prefix => name.startsWith(prefix))).sort();
      const currentScenario = scenario.select.value;
      scenario.select.replaceChildren(...available.map(name => element('option', { value: name }, name)));
      scenario.select.value = available.includes(currentScenario) ? currentScenario : available.includes('create') ? 'create' : available[0] || '';
      body.replaceChildren();
      const visible = rows.filter(row => !hidden.has(family(row.Engine)));
      const counters = name => engine => countersFor(selected, runtime.select.value, name, engine);
      if (isHistory) {
        renderTrend(body, windowCommits, runtime.select.value, measure, scenario.select.value, engines.filter(engine => !hidden.has(family(engine))), mode.select.value, detail, selectedWindowIndex, index => {
          selectedCommitSha = windowCommits[index].Commit;
          commit.select.value = selectedCommitSha;
          update();
        });
        const current = visible.filter(row => row.Scenario === scenario.select.value);
        if (current.length) body.append(comparison(scenario.select.value, current, mode.select.value === 'relative', detail, rows.find(row => row.Scenario === scenario.select.value && row.Engine === 'Ranvier')?.Value, measure, counters(scenario.select.value)));
      } else {
        for (const name of available.filter(name => name.toLowerCase().includes(search.value.trim().toLowerCase()))) {
          const matching = visible.filter(row => row.Scenario === name);
          if (matching.length) body.append(comparison(name, matching, mode.select.value === 'relative', detail, rows.find(row => row.Scenario === name && row.Engine === 'Ranvier')?.Value, measure, counters(name)));
        }
      }
      if (!body.children.length) body.append(element('p', { class: 'rv-perf-empty' }, 'No matching measurements. Adjust the filters or show a library.'));
      detail.textContent = 'Hover or focus a mark for exact values. Each comparison panel scales from zero to its largest value.';
      if (focusedKey) [...root.querySelectorAll('[data-focus-key]')].find(node => node.getAttribute('data-focus-key') === focusedKey)?.focus({ preventScroll: true });
      if (shared) {
        shared.runtime = runtime.select.value;
        shared.metric = metric.select.value;
        if (isHistory) shared.from = fromChoice;
      }
      if (isHistory && fromChangedByUser) syncUrlFrom(fromChoice, commits);
    }

    commit.select.addEventListener('change', () => {
      selectedCommitSha = commit.select.value;
      update();
    });
    if (isHistory) from.select.addEventListener('change', () => {
      fromChangedByUser = true;
      update();
    });
    for (const control of [runtime.select, metric.select, mode.select, scenario.select]) control.addEventListener('change', update);
    search.addEventListener('input', update);
    update();
    return {
      // Adopts shared runtime, metric, and From window when another view updated them.
      sync() {
        if (!shared) return;
        const fromChanged = isHistory && shared.from && shared.from !== from.select.value;
        if (shared.runtime === runtime.select.value && shared.metric === metric.select.value && !fromChanged) return;
        if (shared.runtime && shared.runtime !== runtime.select.value) {
          hidden.clear();
          runtime.select.value = shared.runtime;
        }
        previousRuntime = runtime.select.value;
        const next = metricControl(commits, runtime.select.value, shared.metric || metric.select.value);
        metric.wrapper.replaceWith(next.wrapper);
        metric = next;
        metric.select.addEventListener('change', update);
        if (fromChanged) from.select.value = resolveFrom(commits, shared.from).choice;
        update();
      },
    };
  }

  function renderTrend(body, commits, runtime, metric, scenario, engines, valueMode, detail, selected, onSelect) {
    const unit = metrics[metric].unit;
    const relative = valueMode === 'relative';
    const changeRequested = valueMode === 'change';
    const rawValueFor = (commit, engine) => {
      const rows = measurements(commit, runtime, metric).filter(row => row.Scenario === scenario);
      return shown(rows.find(row => row.Engine === engine)?.Value);
    };
    const ranvierBase = commits.map(commit => rawValueFor(commit, 'Ranvier')).find(value => value !== null);
    const change = changeRequested && ranvierBase >= 1;
    const rawSeries = engines.map(engine => {
      const raw = commits.map(commit => rawValueFor(commit, engine));
      const base = raw.find(value => value !== null);
      const plotted = raw.map((value, index) => {
        if (value === null) return null;
        if (change) return base >= 1 ? (value / base - 1) * 100 : null;
        if (relative) {
          const rBase = rawValueFor(commits[index], 'Ranvier');
          return rBase > 0 ? value / rBase : null;
        }
        return value;
      });
      return { engine, raw, plotted };
    });
    const allPlotted = rawSeries.flatMap(item => item.plotted.filter(value => value !== null));
    let low = 0;
    let high = Math.max(Number.MIN_VALUE, ...allPlotted) * 1.12;
    if (change) {
      low = allPlotted.length ? Math.min(-2, ...allPlotted) : -2;
      high = allPlotted.length ? Math.max(2, ...allPlotted) : 2;
      const pad = (high - low) * 0.1;
      low -= pad;
      high += pad;
    }

    const svg = makeSvg(`${scenario}: ${metrics[metric].label.toLowerCase()} history. Select a commit to inspect its library comparison.`, 300);
    const left = 62, right = 608, top = 25, bottom = 240;
    const x = index => commits.length === 1 ? (left + right) / 2 : left + index / (commits.length - 1) * (right - left);
    const y = value => bottom - (value - low) / (high - low) * (bottom - top);
    const yTicks = change ? [low, 0, high].filter(tick => tick === 0 || Math.abs(y(tick) - y(0)) >= 24) : [0, 1, 2, 3, 4].map(t => high * t / 4);
    for (const value of yTicks) {
      svg.append(svgElement('line', { x1: left, x2: right, y1: y(value), y2: y(value), class: change && value === 0 ? 'rv-perf-reference' : 'rv-perf-grid' }));
      const tickLabel = change ? percent(value)
        : relative ? `${ratio(value)}×`
        : Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
      svg.append(svgElement('text', { x: left - 10, y: y(value) + 4, 'text-anchor': 'end', class: 'rv-perf-tick' }, tickLabel));
    }
    svg.append(svgElement('line', { x1: x(selected), x2: x(selected), y1: top, y2: bottom, class: 'rv-perf-selected' }));
    if (relative && high >= 1) svg.append(svgElement('line', { x1: left, x2: right, y1: y(1), y2: y(1), class: 'rv-perf-reference' }));
    rawSeries.forEach(({ engine, raw, plotted: points }) => {
      let path = '', connected = false;
      points.forEach((value, index) => {
        if (value === null) { connected = false; return; }
        path += `${connected ? 'L' : 'M'}${x(index)},${y(value)} `;
        connected = true;
      });
      if (path) svg.append(svgElement('path', { d: path, stroke: colour(engine), fill: 'none', 'stroke-width': 2.2, 'stroke-linejoin': 'round' }));
      points.forEach((value, index) => {
        if (value === null) return;
        const dot = svgElement('g', { class: 'rv-perf-point', role: 'button', 'data-focus-key': JSON.stringify(['point', commits[index].Commit, engine]) });
        dot.append(svgElement('circle', { cx: x(index), cy: y(value), r: 11, fill: 'transparent', stroke: 'none' }));
        dot.append(svgElement('circle', { cx: x(index), cy: y(value), r: index === selected ? 5 : 3.5, fill: colour(engine), class: 'rv-perf-dot' }));
        const formatted = change ? `${number(raw[index])} ${unit} (${percent(value)})`
          : relative ? `${ratio(value)}× Ranvier`
          : `${number(value)} ${unit}`;
        inspectable(dot, `${short(commits[index])} · ${commits[index].Subject} · ${engine}: ${formatted}`, detail);
        dot.addEventListener('click', () => onSelect(index));
        dot.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelect(index); } });
        svg.append(dot);
      });
    });
    const stride = Math.max(1, Math.ceil(commits.length / 6));
    commits.forEach((commit, index) => {
      if (index % stride && index !== commits.length - 1) return;
      const label = svgElement('text', { x: x(index), y: bottom + 24, 'text-anchor': 'middle', class: 'rv-perf-tick rv-perf-commit', tabindex: 0, role: 'button', 'data-focus-key': JSON.stringify(['commit', commit.Commit]), 'aria-label': `Inspect ${short(commit)}: ${commit.Subject}` }, short(commit));
      label.addEventListener('click', () => onSelect(index));
      label.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelect(index); } });
      svg.append(label);
    });
    const footer = change ? 'Change since From · missing values leave gaps'
      : relative ? 'Relative to Ranvier at each commit · missing values leave gaps'
      : `${metrics[metric].label} (${unit}) · lower is better · missing values leave gaps`;
    svg.append(svgElement('text', { x: left, y: 290, class: 'rv-perf-tick' }, footer));
    body.append(svg);
    if (!allPlotted.length) body.append(element('p', { class: 'rv-perf-empty' }, changeRequested && !change ? `Change view requires Ranvier baseline ≥ 1 ${unit}.` : 'No visible measurements for this scenario.'));
  }

  function mountTrends(root, commits, shared) {
    const group = (root.dataset.perfGroup || '').split(',').filter(Boolean);
    const hidden = new Set();
    let activeFilter = 'all';
    let pinnedSha = null;
    let activePanelId = null;
    let activeIndex = null;
    let fromChangedByUser = false;

    root.className = 'rv-perf';
    root.replaceChildren();
    const heading = element('div', { class: 'rv-perf-heading' });
    const title = element('h3', {}, 'Instruction trends');
    heading.append(element('p', { class: 'rv-perf-eyebrow' }, 'Across commits'), title);
    const controls = element('div', { class: 'rv-perf-controls' });
    const runtimes = [...new Set(commits.flatMap(commit => commit.Medians.filter(row => row.Leg === 'arm64-counters' && row.Metric === 'instructions').map(row => row.Runtime)))].sort();
    if (!runtimes.length) throw new Error('No instruction measurements are available yet.');
    const runtime = selectControl('Runtime', runtimes.map(key => [key, names[key] || key]), runtimes.includes(shared?.runtime) ? shared.runtime : runtimes.includes('dotnet') ? 'dotnet' : runtimes[0]);
    let metric = metricControl(commits, runtime.select.value, shared?.metric || 'instructions');
    const from = fromControl(commits, shared?.from ?? urlFromChoice());
    const mode = selectControl('Values', [['change', 'Change since From'], ['absolute', 'Per operation']], 'change');
    const order = selectControl('Order', [
      ['name', 'Scenario name'],
      ['change', 'Biggest net change'],
      ['regressions', 'Regressions first'],
      ['improvements', 'Improvements first'],
      ['step', 'Biggest commit step'],
    ], 'name');
    controls.append(runtime.wrapper, metric.wrapper, from.wrapper, mode.wrapper, order.wrapper);
    const legend = element('div', { class: 'rv-perf-legend', 'aria-label': 'Reference libraries' });
    const provenance = element('div', { class: 'rv-perf-provenance' });
    const summaryBar = element('div', { class: 'rv-perf-summary', role: 'toolbar', 'aria-label': 'Filter scenarios by movement' });
    const inspectBar = element('div', { class: 'rv-perf-inspect rv-perf-inspect--sticky', role: 'status', 'aria-live': 'polite' });
    const body = element('div', { class: 'rv-perf-panels' });
    root.append(heading, controls, legend, provenance, summaryBar, inspectBar, body);

    let previousRuntime = runtime.select.value;
    let previousMetric = metric.select.value;
    let controllers = [];
    let currentWindow = commits;

    function clearInspection() {
      for (const ctrl of controllers) ctrl.hide();
      const latest = currentWindow.at(-1);
      const head = element('div', { class: 'rv-perf-inspect-head' });
      const titleLine = element('span');
      titleLine.append(document.createTextNode('Latest: '), commitLink(latest), document.createTextNode(` · ${fullDate(latest)} · ${latest.Subject}`));
      head.append(titleLine);
      const hint = element('div', { class: 'rv-perf-inspect-body' }, 'Hover a panel or use arrow keys to scrub all charts; click a commit to pin it or set From.');
      inspectBar.replaceChildren(head, hint);
    }

    function setInspection(index, targetPanelId, pinned) {
      if (!currentWindow.length) return;
      const clamped = Math.max(0, Math.min(currentWindow.length - 1, index));
      activeIndex = clamped;
      activePanelId = targetPanelId || controllers[0]?.id || null;
      const targetCtrl = controllers.find(ctrl => ctrl.id === activePanelId) || controllers[0];
      for (const ctrl of controllers) ctrl.showAt(clamped, ctrl === targetCtrl, pinned);

      const inspectedCommit = currentWindow[clamped];
      const head = element('div', { class: 'rv-perf-inspect-head' });
      const leftMeta = element('span');
      if (pinned) leftMeta.append(element('span', { class: 'rv-perf-badge' }, 'Pinned'), document.createTextNode(' '));
      leftMeta.append(commitLink(inspectedCommit), document.createTextNode(` · ${fullDate(inspectedCommit)} · ${inspectedCommit.Subject}`));
      head.append(leftMeta);

      const actions = element('div', { class: 'rv-perf-inspect-actions' });
      if (clamped > 0) {
        const anchorBtn = element('button', { type: 'button', class: 'rv-perf-action' }, `Start from ${short(inspectedCommit)}`);
        anchorBtn.addEventListener('click', () => {
          from.select.value = inspectedCommit.Commit;
          pinnedSha = null;
          fromChangedByUser = true;
          update();
        });
        actions.append(anchorBtn);
      }
      if (pinned) {
        const unpinBtn = element('button', { type: 'button', class: 'rv-perf-action' }, 'Unpin');
        unpinBtn.addEventListener('click', () => {
          pinnedSha = null;
          clearInspection();
        });
        actions.append(unpinBtn);
      }
      if (actions.childNodes.length) head.append(actions);

      const detailLine = element('div', { class: 'rv-perf-inspect-body' });
      if (targetCtrl) targetCtrl.renderInspect(clamped, detailLine);
      inspectBar.replaceChildren(head, detailLine);
    }

    body.addEventListener('pointerleave', () => {
      if (pinnedSha || body.contains(document.activeElement)) return;
      clearInspection();
    });

    function update() {
      if (previousRuntime !== runtime.select.value) {
        hidden.clear();
        activeFilter = 'all';
        const next = metricControl(commits, runtime.select.value, metric.select.value);
        metric.wrapper.replaceWith(next.wrapper);
        metric = next;
        metric.select.addEventListener('change', update);
      }
      const measure = metric.select.value;
      if (previousMetric !== measure) activeFilter = 'all';
      previousRuntime = runtime.select.value;
      previousMetric = measure;
      title.textContent = metrics[measure].trends;

      const resolved = resolveFrom(commits, from.select.value);
      from.select.value = resolved.choice;
      const fromIndex = resolved.index;
      currentWindow = commits.slice(fromIndex);
      const priorCommit = fromIndex > 0 ? commits[fromIndex - 1] : null;
      if (pinnedSha && !currentWindow.some(commit => commit.Commit === pinnedSha)) pinnedSha = null;

      const fullPerCommit = commits.map(commit => measurements(commit, runtime.select.value, measure));
      const windowPerCommit = fullPerCommit.slice(fromIndex);
      const inGroup = name => !group.length || group.some(prefix => name.startsWith(prefix));
      const rows = windowPerCommit.flat().filter(row => inGroup(row.Scenario));
      const references = [...new Set(enginesIn(rows).map(family))].filter(name => name !== 'Ranvier');
      if (JSON.stringify(references) !== legend.dataset.families) {
        legend.replaceChildren(...references.map(engine => engineToggle(engine, hidden, update)));
        legend.dataset.families = JSON.stringify(references);
      }
      [...legend.children].forEach((button, index) => button.setAttribute('aria-pressed', String(!hidden.has(references[index]))));

      const firstCommit = currentWindow[0];
      const lastCommit = currentWindow.at(-1);
      provenance.replaceChildren(
        document.createTextNode(currentWindow.length === commits.length
          ? `${commits.length} recorded commits · `
          : `Showing ${currentWindow.length} of ${commits.length} commits · `),
        commitLink(firstCommit),
        document.createTextNode(` (${fullDate(firstCommit)}) → `),
        commitLink(lastCommit),
        document.createTextNode(` (${fullDate(lastCommit)})`),
      );
      if (currentWindow.length > 1) {
        provenance.append(
          document.createTextNode(' · '),
          element('a', { href: `https://github.com/ranvier-org/Ranvier/compare/${firstCommit.Commit}...${lastCommit.Commit}`, class: 'rv-perf-compare-link' }, 'Compare diff ↗'),
        );
      }
      const startVer = ranvierVersion(firstCommit);
      const endVer = ranvierVersion(lastCommit);
      if (startVer && endVer && startVer !== endVer) provenance.append(document.createTextNode(` · v${startVer} → v${endVer}`));
      if (resolved.choice !== defaultFromChoice(commits)) {
        const resetButton = element('button', { type: 'button', class: 'rv-perf-action' }, 'Reset window');
        resetButton.addEventListener('click', () => {
          from.select.value = defaultFromChoice(commits);
          pinnedSha = null;
          fromChangedByUser = true;
          update();
        });
        provenance.append(document.createTextNode(' · '), resetButton);
      }

      const items = [];
      for (const scenario of [...new Set(rows.map(row => row.Scenario))].sort()) {
        const engines = enginesIn(rows.filter(row => row.Scenario === scenario));
        const others = engines.filter(engine => family(engine) !== 'Ranvier' && !hidden.has(family(engine)));
        for (const subject of engines.filter(engine => family(engine) === 'Ranvier')) {
          const subjectValues = windowPerCommit.map(list => shown(list.find(row => row.Scenario === scenario && row.Engine === subject)?.Value));
          const recorded = subjectValues.map((value, index) => ({ value, index })).filter(point => point.value !== null);
          const first = recorded[0];
          const last = recorded.at(-1);
          const netPct = first && last && last.index > first.index && first.value >= 1 ? (last.value / first.value - 1) * 100 : 0;
          let maxStepAbsPct = 0;
          for (let k = 1; k < recorded.length; k++) {
            if (recorded[k - 1].value >= 1) maxStepAbsPct = Math.max(maxStepAbsPct, Math.abs((recorded[k].value / recorded[k - 1].value - 1) * 100));
          }
          const bucket = netPct <= -SIGNIFICANT_PCT ? 'improved' : netPct >= SIGNIFICANT_PCT ? 'regressed' : 'steady';
          items.push({ id: `${scenario}\u0000${subject}`, scenario, subject, others, netPct, maxStepAbsPct, bucket });
        }
      }

      const improvedCount = items.filter(item => item.bucket === 'improved').length;
      const regressedCount = items.filter(item => item.bucket === 'regressed').length;
      const steadyCount = items.filter(item => item.bucket === 'steady').length;
      const filters = [
        ['all', `All (${items.length})`],
        ['improved', `Improved ≤ −5% (${improvedCount})`],
        ['regressed', `Regressed ≥ +5% (${regressedCount})`],
        ['steady', `Within ±5% (${steadyCount})`],
      ];
      summaryBar.replaceChildren(...filters.map(([key, label]) => {
        const active = activeFilter === key;
        const pill = element('button', { type: 'button', class: 'rv-perf-filter', 'aria-pressed': String(active) }, label);
        pill.addEventListener('click', () => {
          activeFilter = key !== 'all' && activeFilter === key ? 'all' : key;
          update();
        });
        return pill;
      }));
      summaryBar.hidden = items.length === 0;

      const filtered = activeFilter === 'all' ? items : items.filter(item => item.bucket === activeFilter);
      const sortBy = order.select.value;
      const sorted = [...filtered].sort((a, b) => {
        if (sortBy === 'change') return Math.abs(b.netPct) - Math.abs(a.netPct) || a.scenario.localeCompare(b.scenario);
        if (sortBy === 'regressions') return b.netPct - a.netPct || a.scenario.localeCompare(b.scenario);
        if (sortBy === 'improvements') return a.netPct - b.netPct || a.scenario.localeCompare(b.scenario);
        if (sortBy === 'step') return b.maxStepAbsPct - a.maxStepAbsPct || Math.abs(b.netPct) - Math.abs(a.netPct) || a.scenario.localeCompare(b.scenario);
        return a.scenario.localeCompare(b.scenario) || rank(a.subject) - rank(b.subject);
      });

      const handlers = {
        onScrub: (index, id) => {
          if (pinnedSha) return;
          setInspection(index, id, false);
        },
        onStep: (index, id) => {
          if (pinnedSha) {
            const clamped = Math.max(0, Math.min(currentWindow.length - 1, index));
            pinnedSha = currentWindow[clamped].Commit;
            setInspection(clamped, id, true);
          } else {
            setInspection(index, id, false);
          }
        },
        onPinToggle: (index, id) => {
          const clamped = Math.max(0, Math.min(currentWindow.length - 1, index));
          const targetSha = currentWindow[clamped].Commit;
          if (pinnedSha === targetSha && activePanelId === id) {
            pinnedSha = null;
            setInspection(clamped, id, false);
          } else {
            pinnedSha = targetSha;
            setInspection(clamped, id, true);
          }
        },
        onUnpin: () => {
          if (!pinnedSha) return;
          pinnedSha = null;
          clearInspection();
        },
        onLeave: nextFocus => {
          if (pinnedSha || body.matches(':hover') || body.contains(document.activeElement)
            || (nextFocus instanceof Node && (body.contains(nextFocus) || inspectBar.contains(nextFocus)))) return;
          clearInspection();
        },
      };

      controllers = sorted.map(item => trendPanel({
        id: item.id,
        scenario: item.scenario,
        subject: item.subject,
        others: item.others,
        commits,
        windowCommits: currentWindow,
        fromIndex,
        priorCommit,
        fullPerCommit,
        windowPerCommit,
        changeRequested: mode.select.value === 'change',
        runtime: runtime.select.value,
        metric: measure,
        handlers,
      }));

      body.replaceChildren(...controllers.map(ctrl => ctrl.element));
      if (!controllers.length) {
        const message = items.length ? 'No scenarios match this movement filter in the current window.' : 'No recorded measurements for these scenarios on this runtime.';
        body.append(element('p', { class: 'rv-perf-empty' }, message));
      }

      if (pinnedSha) {
        const pIdx = currentWindow.findIndex(commit => commit.Commit === pinnedSha);
        if (pIdx >= 0) setInspection(pIdx, activePanelId, true);
        else {
          pinnedSha = null;
          clearInspection();
        }
      } else {
        clearInspection();
      }

      if (shared) {
        shared.runtime = runtime.select.value;
        shared.metric = measure;
        shared.from = resolved.choice;
      }
      if (fromChangedByUser) syncUrlFrom(resolved.choice, commits);
    }

    from.select.addEventListener('change', () => {
      fromChangedByUser = true;
      update();
    });
    for (const control of [runtime.select, metric.select, mode.select, order.select]) control.addEventListener('change', update);
    update();
    return {
      // Adopts shared runtime, metric, and From window when another view updated them.
      sync() {
        if (!shared) return;
        const fromChanged = shared.from && shared.from !== from.select.value;
        if (shared.runtime === runtime.select.value && shared.metric === metric.select.value && !fromChanged) return;
        if (shared.runtime && shared.runtime !== runtime.select.value) {
          hidden.clear();
          runtime.select.value = shared.runtime;
        }
        previousRuntime = runtime.select.value;
        const next = metricControl(commits, runtime.select.value, shared.metric || metric.select.value);
        metric.wrapper.replaceWith(next.wrapper);
        metric = next;
        metric.select.addEventListener('change', update);
        if (fromChanged) from.select.value = resolveFrom(commits, shared.from).choice;
        update();
      },
    };
  }

  function trendPanel({ id, scenario, subject, others, commits, windowCommits, fromIndex, priorCommit, fullPerCommit, windowPerCommit, changeRequested, runtime, metric, handlers }) {
    const unit = metrics[metric].unit;
    const rowsFor = (list, engine) => list.map(rows => rows.find(row => row.Scenario === scenario && row.Engine === engine));
    const firstOf = values => values.find(value => value !== null);
    const subjectFullRaw = rowsFor(fullPerCommit, subject).map(row => shown(row?.Value));
    const subjectBase = firstOf(rowsFor(windowPerCommit, subject).map(row => shown(row?.Value)));
    const change = changeRequested && subjectBase >= 1;
    const series = [subject, ...others].map(engine => {
      const rows = rowsFor(windowPerCommit, engine);
      const raw = rows.map(row => shown(row?.Value));
      const base = firstOf(raw);
      const scale = value => value === null ? null : change ? base >= 1 ? (value / base - 1) * 100 : null : value;
      const band = rows.map(row => Number.isFinite(row?.Min) ? [scale(shown(row.Min)), scale(shown(row.Max))] : null);
      return { engine, raw, rows, band, plotted: raw.map(scale) };
    }).filter(item => item.plotted.some(value => value !== null));

    const prevRecordedInWindow = (rawValues, index) => {
      for (let i = index - 1; i >= 0; i--) if (rawValues[i] !== null) return { index: i, value: rawValues[i] };
      return null;
    };
    const prevRecordedInFull = windowIdx => {
      for (let i = fromIndex + windowIdx - 1; i >= 0; i--) if (subjectFullRaw[i] !== null) return { fullIndex: i, value: subjectFullRaw[i] };
      return null;
    };

    const subjectRows = rowsFor(windowPerCommit, subject);
    const counters = windowCommits.map(commit => countersFor(commit, runtime, scenario, subject));
    const priorCounter = priorCommit ? countersFor(priorCommit, runtime, scenario, subject) : '';
    const priorCounted = Boolean(priorCommit && fullPerCommit[fromIndex - 1]?.some(row => row.Scenario === scenario && row.Engine === subject) && (priorCommit.Counters || []).length > 0);
    const counted = index => subjectRows[index] !== undefined && (windowCommits[index].Counters || []).length > 0;
    const countersChanged = counters.map((value, index) => {
      if (!counted(index)) return false;
      if (index === 0) return priorCounted && value !== priorCounter;
      return counted(index - 1) && value !== counters[index - 1];
    });
    const acceptedList = windowCommits.map(commit => acceptedFor(commit, runtime, scenario, subject, metric));

    const variant = subject.includes(' (') ? subject.slice(subject.indexOf(' (') + 2, -1) : '';
    const name = variant ? `${scenario} · ${variant}` : scenario;
    const panel = element('section', { class: 'rv-perf-panel rv-perf-spark' });
    const header = element('header');
    header.append(element('h4', {}, name));

    const recorded = (series[0]?.engine === subject ? series[0].raw : []).map((value, index) => ({ value, index })).filter(point => point.value !== null);
    if (recorded.length) {
      const first = recorded[0], last = recorded.at(-1), previous = recorded.at(-2);
      const summary = element('span');
      summary.append(document.createTextNode(`${number(last.value)} ${unit} · `));
      if (recorded.length === 1) {
        summary.append(document.createTextNode(`at ${short(windowCommits[first.index])}`));
      } else if (first.value >= 1) {
        appendDeltaBadge(summary, (last.value / first.value - 1) * 100, ` since ${short(windowCommits[first.index])}`);
      } else {
        const diff = last.value - first.value;
        const sign = number(last.value) === number(first.value) ? '±' : diff > 0 ? '+' : '−';
        summary.append(document.createTextNode(`${sign}${number(Math.abs(diff))} since ${short(windowCommits[first.index])}`));
      }
      if (previous) {
        const stepText = previous.value >= 1
          ? percent((last.value / previous.value - 1) * 100)
          : `${number(last.value) === number(previous.value) ? '±' : last.value > previous.value ? '+' : '−'}${number(Math.abs(last.value - previous.value))}`;
        summary.append(document.createTextNode(` · ${stepText} latest`));
      }
      if (changeRequested && !change) summary.append(document.createTextNode(` · absolute: baseline below 1 ${unit}`));
      header.append(summary);
    }
    panel.append(header);

    const left = 96, right = 628, top = 16, bottom = 172;
    const plotted = series.flatMap(item => [...item.plotted, ...(item.engine === subject ? item.band.flat() : [])]).filter(value => value !== null && value !== undefined);
    let low = 0, high = (Math.max(0, ...plotted) || 1) * 1.1;
    if (change) {
      low = Math.min(-2, ...plotted);
      high = Math.max(2, ...plotted);
      const pad = (high - low) * 0.1;
      low -= pad;
      high += pad;
    }
    const x = index => windowCommits.length === 1 ? (left + right) / 2 : left + index / (windowCommits.length - 1) * (right - left);
    const y = value => bottom - (value - low) / (high - low) * (bottom - top);
    const quantity = `${metrics[metric].label.toLowerCase()} per operation`;
    const svg = makeSvg(`${name}: ${change ? `change in ${quantity} since From` : quantity} across ${windowCommits.length} commits`, 212);
    svg.setAttribute('tabindex', '0');
    svg.setAttribute('role', 'img');
    svg.append(svgElement('rect', { x: 0, y: 0, width: 640, height: 212, fill: 'transparent' }));
    const compact = value => high < 10 ? number(value) : Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
    for (const tick of change ? [low, 0, high].filter(tick => tick === 0 || Math.abs(y(tick) - y(0)) >= 24) : [0, high / 2, high]) {
      svg.append(svgElement('line', { x1: left, x2: right, y1: y(tick), y2: y(tick), class: change && tick === 0 ? 'rv-perf-reference' : 'rv-perf-grid' }));
      svg.append(svgElement('text', { x: left - 10, y: y(tick) + 7, 'text-anchor': 'end', class: 'rv-perf-spark-tick' }, change ? percent(tick) : compact(tick)));
    }
    // Lowest-to-highest head-process spread for Ranvier, drawn across consecutive recorded commits.
    const spread = series.find(item => item.engine === subject)?.band || [];
    let run = [];
    const closeRun = () => {
      if (run.length > 1) {
        const upper = run.map(index => `${x(index)},${y(spread[index][1])}`);
        const lower = run.map(index => `${x(index)},${y(spread[index][0])}`).reverse();
        svg.append(svgElement('path', { d: `M${upper.join(' L')} L${lower.join(' L')} Z`, fill: colour(subject), class: 'rv-perf-spread' }));
      }
      run = [];
    };
    spread.forEach((range, index) => range && range[0] !== null ? run.push(index) : closeRun());
    closeRun();

    windowCommits.forEach((_commit, index) => {
      const hasCounter = countersChanged[index];
      const hasAccepted = acceptedList[index].length > 0;
      const cx = x(index);
      if (hasCounter) {
        const mx = hasAccepted ? cx - 7 : cx;
        svg.append(svgElement('path', { d: `M${mx},${bottom + 2} l-6,10 h12 Z`, class: 'rv-perf-counter-change' }));
      }
      if (hasAccepted) {
        const mx = hasCounter ? cx + 7 : cx;
        svg.append(svgElement('path', { d: `M${mx},${bottom + 2} l5,5 l-5,5 l-5,-5 Z`, class: 'rv-perf-accepted-mark' }));
      }
    });

    const subjectSeries = series[0]?.engine === subject ? series[0] : null;
    [...series].reverse().forEach(({ engine, raw, plotted: points }) => {
      const isSubject = engine === subject;
      let path = '', connected = false;
      points.forEach((value, index) => {
        if (value === null) { connected = false; return; }
        path += `${connected ? 'L' : 'M'}${x(index)},${y(value)} `;
        connected = true;
      });
      if (path) svg.append(svgElement('path', { d: path, stroke: colour(engine), fill: 'none', 'stroke-width': isSubject ? 2.5 : 1.75, 'stroke-linejoin': 'round', opacity: isSubject ? 1 : 0.7 }));
      points.forEach((value, index) => {
        if (value === null) return;
        const isolated = (index === 0 || points[index - 1] === null) && (index === points.length - 1 || points[index + 1] === null);
        if (!isSubject) {
          if (isolated) svg.append(svgElement('circle', { cx: x(index), cy: y(value), r: 2.5, fill: colour(engine), opacity: 0.85 }));
          return;
        }
        const prev = prevRecordedInWindow(raw, index);
        const stepPct = prev && prev.value >= 1 ? (raw[index] / prev.value - 1) * 100 : 0;
        const significantStep = Math.abs(stepPct) >= SIGNIFICANT_PCT;
        if (significantStep) {
          const stepCls = `rv-perf-dot rv-perf-step ${stepPct < 0 ? 'rv-perf-step--improved' : 'rv-perf-step--regressed'}`;
          svg.append(svgElement('circle', { cx: x(index), cy: y(value), r: 3.8, class: stepCls }));
        } else if (windowCommits.length <= 40 || isolated) {
          svg.append(svgElement('circle', { cx: x(index), cy: y(value), r: 2.2, fill: colour(engine), opacity: 0.75 }));
        }
      });
      const lastIndex = points.findLastIndex(value => value !== null);
      if (isSubject && lastIndex >= 0) svg.append(svgElement('circle', { cx: x(lastIndex), cy: y(points[lastIndex]), r: 4.5, fill: colour(engine), class: 'rv-perf-dot' }));
    });

    svg.append(svgElement('text', { x: left, y: bottom + 32, class: 'rv-perf-spark-tick' }, `${short(windowCommits[0])} · ${shortDate(windowCommits[0])}`));
    if (windowCommits.length >= 6) {
      const midIndex = Math.floor((windowCommits.length - 1) / 2);
      svg.append(svgElement('text', { x: x(midIndex), y: bottom + 32, 'text-anchor': 'middle', class: 'rv-perf-spark-tick' }, short(windowCommits[midIndex])));
    }
    if (windowCommits.length > 1) {
      svg.append(svgElement('text', { x: right, y: bottom + 32, 'text-anchor': 'end', class: 'rv-perf-spark-tick' }, `${short(windowCommits.at(-1))} · ${shortDate(windowCommits.at(-1))}`));
    }

    const crosshair = svgElement('line', { y1: top, y2: bottom, class: 'rv-perf-crosshair', visibility: 'hidden' });
    const markers = svgElement('g', { visibility: 'hidden' });
    svg.append(crosshair, markers);

    let current = windowCommits.length - 1;
    const showAt = (index, active, pinned) => {
      current = Math.max(0, Math.min(windowCommits.length - 1, index));
      panel.classList.toggle('is-active', Boolean(active));
      panel.classList.toggle('is-pinned', Boolean(active && pinned));
      crosshair.setAttribute('x1', x(current));
      crosshair.setAttribute('x2', x(current));
      crosshair.setAttribute('visibility', 'visible');
      markers.replaceChildren(...series.filter(item => item.plotted[current] !== null)
        .map(item => svgElement('circle', { cx: x(current), cy: y(item.plotted[current]), r: active ? 5 : 4, fill: colour(item.engine), class: 'rv-perf-dot' })));
      markers.setAttribute('visibility', 'visible');
    };
    const hide = () => {
      panel.classList.remove('is-active', 'is-pinned');
      crosshair.setAttribute('visibility', 'hidden');
      markers.setAttribute('visibility', 'hidden');
    };
    const renderInspect = (index, container) => {
      container.replaceChildren();
      container.append(element('strong', {}, name), document.createTextNode(' — '));
      series.forEach((item, seriesIndex) => {
        if (seriesIndex > 0) container.append(document.createTextNode(' · '));
        if (item.raw[index] === null) {
          container.append(document.createTextNode(`${item.engine}: not recorded`));
          return;
        }
        container.append(document.createTextNode(`${item.engine}: ${number(item.raw[index])} ${unit}`));
        const notes = [];
        if (item.engine === subject) {
          const prev = prevRecordedInFull(index);
          if (prev && prev.value >= 1) {
            const stepPct = (item.raw[index] / prev.value - 1) * 100;
            notes.push({ label: 'Δ prev: ', deltaPct: stepPct });
          }
          if (recorded.length && index > recorded[0].index && recorded[0].value >= 1) {
            const fromPct = (item.raw[index] / recorded[0].value - 1) * 100;
            notes.push({ label: `Δ From (${short(windowCommits[recorded[0].index])}): `, deltaPct: fromPct });
          }
          const range = processRange(item.rows[index]);
          if (range) notes.push({ text: range });
        } else if (change && item.plotted[index] !== null) {
          notes.push({ label: '', deltaPct: item.plotted[index] });
        }
        if (notes.length) {
          container.append(document.createTextNode(' ('));
          notes.forEach((entry, noteIndex) => {
            if (noteIndex > 0) container.append(document.createTextNode(', '));
            if (entry.text !== undefined) container.append(document.createTextNode(entry.text));
            else {
              container.append(document.createTextNode(entry.label));
              appendDeltaBadge(container, entry.deltaPct);
            }
          });
          container.append(document.createTextNode(')'));
        }
      });
      if (counters[index]) {
        const prefix = countersChanged[index] ? ' · ▲ library counters changed: ' : ' · library counters: ';
        container.append(document.createTextNode(`${prefix}${counters[index]}`));
      }
      const accepted = acceptedList[index];
      if (accepted.length) {
        const reason = accepted.map(item => item.Reason).filter(Boolean).join('; ') || 'accepted';
        container.append(document.createTextNode(` · ◆ accepted: ${reason}`));
      }
    };
    const indexAt = event => {
      if (windowCommits.length <= 1) return 0;
      const bounds = svg.getBoundingClientRect();
      const viewX = (event.clientX - bounds.left) / bounds.width * 640;
      return Math.max(0, Math.min(windowCommits.length - 1, Math.round((viewX - left) / (right - left) * (windowCommits.length - 1))));
    };

    svg.addEventListener('pointermove', event => handlers.onScrub(indexAt(event), id));
    svg.addEventListener('pointerdown', event => { current = indexAt(event); });
    svg.addEventListener('click', event => handlers.onPinToggle(indexAt(event), id));
    svg.addEventListener('focus', () => handlers.onScrub(current, id));
    svg.addEventListener('blur', event => handlers.onLeave(event.relatedTarget));
    svg.addEventListener('keydown', event => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        handlers.onPinToggle(current, id);
        return;
      }
      if (event.key === 'Escape') {
        event.preventDefault();
        handlers.onUnpin();
        return;
      }
      const target = { ArrowLeft: current - 1, ArrowRight: current + 1, Home: 0, End: windowCommits.length - 1 }[event.key];
      if (target === undefined) return;
      event.preventDefault();
      handlers.onStep(target, id);
    });
    panel.append(svg);
    return { id, element: panel, showAt, hide, renderInspect };
  }

  const views = [['compare', 'Library comparison', mount], ['trends', 'Trends', mountTrends]];
  const tabKey = 'ranvier.perf.tab';
  const tabGroups = [];
  const storedTab = () => { try { return localStorage.getItem(tabKey); } catch { return null; } };
  const storeTab = key => { try { localStorage.setItem(tabKey, key); } catch { } };
  const hashTab = () => views.find(([key]) => location.hash === `#perf-${key}`)?.[0];
  // Comparison and trend views of one scenario group under a tablist. Each view mounts when first
  // shown and adopts shared runtime, metric and From selections.
  function mountTabs(root, commits) {
    const shared = { from: urlFromChoice() || undefined };
    const anchored = !tabGroups.length;
    const prefix = `rv-perf-${tabGroups.length}`;
    root.className = 'rv-perf-tabs';
    root.replaceChildren();
    const list = element('div', { role: 'tablist', class: 'rv-perf-tablist', 'aria-label': 'Measurement view' });
    const items = views.map(([key, label, mountView]) => {
      const tab = element('button', { type: 'button', role: 'tab', class: 'rv-perf-tab', id: anchored ? `perf-${key}` : `${prefix}-${key}`, 'aria-controls': `${prefix}-${key}-panel` }, label);
      const pane = element('div', { role: 'tabpanel', id: `${prefix}-${key}-panel`, 'aria-labelledby': tab.id });
      if (root.dataset.perfGroup) pane.dataset.perfGroup = root.dataset.perfGroup;
      tab.addEventListener('click', () => choose(key, false));
      list.append(tab);
      return { key, tab, pane, mountView, view: null };
    });
    root.append(list, ...items.map(item => item.pane));
    function select(key, focus) {
      for (const item of items) {
        const active = item.key === key;
        item.tab.setAttribute('aria-selected', String(active));
        item.tab.tabIndex = active ? 0 : -1;
        item.pane.hidden = !active;
        if (!active) continue;
        if (item.view) item.view.sync();
        else {
          try { item.view = item.mountView(item.pane, commits, shared); }
          catch (error) {
            item.pane.replaceChildren(element('p', { role: 'status', class: 'rv-perf-empty' }, error.message));
            item.view = { sync() { } };
          }
        }
        if (focus) item.tab.focus();
      }
    }
    function choose(key, focus) {
      storeTab(key);
      for (const group of tabGroups) group === select ? select(key, focus) : group(key, false);
    }
    list.addEventListener('keydown', event => {
      const index = items.findIndex(item => item.tab === document.activeElement);
      const target = { ArrowLeft: index - 1, ArrowRight: index + 1, Home: 0, End: items.length - 1 }[event.key];
      if (index < 0 || target === undefined) return;
      event.preventDefault();
      choose(items[(target + items.length) % items.length].key, true);
    });
    tabGroups.push(select);
    const stored = storedTab();
    select(hashTab() || (views.some(([key]) => key === stored) ? stored : 'compare'), false);
    if (anchored && hashTab()) items.find(item => item.key === hashTab()).tab.scrollIntoView();
  }
  window.addEventListener('hashchange', () => { const key = hashTab(); if (key) for (const group of tabGroups) group(key, false); });

  // Loads `perf-data/<data-perf-data>.json` for scenario groups or `perf-data/index.json` once per page.
  const script = document.currentScript.src;
  const requests = new Map();
  const historyData = name => {
    if (!requests.has(name)) requests.set(name, fetch(new URL(`../perf-data/${name}.json`, script)).then(response => {
      if (!response.ok) throw new Error(`History request failed (${response.status})`);
      return response.json();
    }).then(data => {
      if (!Array.isArray(data.commits) || !data.commits.length) throw new Error('No recorded measurements yet.');
      return [...data.commits].sort((a, b) => Date.parse(a.Date) - Date.parse(b.Date) || a.Commit.localeCompare(b.Commit));
    }));
    return requests.get(name);
  };
  for (const root of roots) {
    const name = /^[a-z0-9-]+$/.test(root.dataset.perfData || '') ? root.dataset.perfData : 'index';
    historyData(name).then(commits => {
      const shared = { from: urlFromChoice() || undefined };
      try { root.hasAttribute('data-perf-tabs') ? mountTabs(root, commits) : root.hasAttribute('data-perf-trends') ? mountTrends(root, commits, shared) : mount(root, commits, shared); }
      catch (error) { root.replaceChildren(element('p', { role: 'status', class: 'rv-perf-empty' }, error.message)); }
    }).catch(error => {
      root.replaceChildren(element('p', { role: 'status', class: 'rv-perf-empty' }, `Performance history unavailable. ${error.message}`));
    });
  }
})();
