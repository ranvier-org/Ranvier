# Collapses three result files into one row per workload, length and strategy:
# the lowest, middle and highest of the three per-run medians, for ns/tick and for ns over bare.
#
# Run from the repository root:
#   awk -f experiments/buffer-handoff-bench/summarise.awk experiments/buffer-handoff-bench/results-run*.txt
function sort3(a, b, c, out,    t) {
    if (a > b) { t = a; a = b; b = t }
    if (b > c) { t = b; b = c; c = t }
    if (a > b) { t = a; a = b; b = t }
    out[1] = a; out[2] = b; out[3] = c
}

/^workload=/ {
    split($1, w, "="); split($2, n, "=")
    key = w[2] " " n[2]
    if (!(key in seen)) { seen[key] = 1; order[++configs] = key }
}

/^(bare|fresh|swap|version|always) / {
    id = key "|" $1
    runs[id]++
    med[id, runs[id]] = $2
    over[id, runs[id]] = $4
    bytes[id] = $6
    g0[id, runs[id]] = $7
    g2[id, runs[id]] = $9
}

END {
    split("bare fresh swap version always", names, " ")
    printf "%-22s %-8s %12s %12s %12s | %12s %12s %12s | %10s %8s %8s\n", "config", "strategy", "ns lo", "ns mid", "ns hi", "over lo", "over mid", "over hi", "B/tick", "gen0/1k", "gen2/1k"
    for (c = 1; c <= configs; c++) {
        for (s = 1; s <= 5; s++) {
            id = order[c] "|" names[s]
            sort3(med[id, 1] + 0, med[id, 2] + 0, med[id, 3] + 0, m)
            sort3(over[id, 1] + 0, over[id, 2] + 0, over[id, 3] + 0, o)
            sort3(g0[id, 1] + 0, g0[id, 2] + 0, g0[id, 3] + 0, a)
            sort3(g2[id, 1] + 0, g2[id, 2] + 0, g2[id, 3] + 0, g)
            printf "%-22s %-8s %12.1f %12.1f %12.1f | %12.1f %12.1f %12.1f | %10.0f %8.3f %8.3f\n", order[c], names[s], m[1], m[2], m[3], o[1], o[2], o[3], bytes[id], a[2], g[2]
        }
    }
}
