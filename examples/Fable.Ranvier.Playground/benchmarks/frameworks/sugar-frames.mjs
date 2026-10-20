// Names Fable gives the template sugar's emitted code, for the allocation comparer and the bundle-size check.
// The tag classes are read from the generated sugar module, so the list follows the generator.
import { readFileSync } from "node:fs";

const generated = new URL("../../../../experiments/Ranvier.View.Portable/Generated/HtmlTemplateSugar.fs", import.meta.url);

/** The generated tag-class names as Fable emits them, without the `$1` suffix (`base'` is `base$0027`). */
export const tagClasses = [...readFileSync(generated, "utf8").matchAll(/^\s*type ([a-z][a-z0-9']*)<'i> private \(ps:/gm)]
  .map(match => match[1].replace("'", "$0027"));
if (tagClasses.length !== 112) throw Error(`Expected 112 tag classes in ${generated.pathname}, found ${tagClasses.length}`);

const escape = name => name.replace(/\$/g, "\\$");

/**
 * Each alternative of the sugar-frame pattern, by name.
 * A tag class `<tag>$1` is matched through its `_$ctor_` and `_$reflection` functions and a bundler-renamed `<tag>$1$<n>`;
 * the bare `<tag>$1` also names unrelated bundler-renamed functions, such as fable-library's `map$1`.
 * `componentTag` appears in no benchmark fixture; `exempt` lists the alternatives a sugared bundle need not contain.
 */
export const sugarAlternatives = {
  element: /\bElement\$\d/,
  voidElement: /\bVoidElement\$\d/,
  componentTag: /\bComponentTag\$\d/,
  tagClass: new RegExp(`(^|[^A-Za-z0-9_$])(${tagClasses.map(escape).join("|")})\\$1(_\\$ctor_|_\\$reflection\\b|\\$\\d)`),
  sugar: /Sugar/,
  delay: /Delay/
};
export const exempt = ["componentTag"];

/** Matches a function or frame name that belongs to the template sugar. */
export const sugarFrame = new RegExp(Object.values(sugarAlternatives).map(pattern => pattern.source).join("|"));

/** The identifiers of a JavaScript source. */
export const identifiers = source => [...new Set(source.match(/[A-Za-z_$][\w$]*/g))];

/**
 * The identifiers each alternative matches in the sugared and the explicit source, and the violations:
 * an alternative outside `exempt` that matches no sugared identifier, or any alternative that matches an explicit identifier.
 */
export function checkAlternatives(sugaredSource, explicitSource) {
  const sugared = identifiers(sugaredSource), explicit = identifiers(explicitSource);
  const counts = {}, violations = [];
  for (const [name, pattern] of Object.entries(sugarAlternatives)) {
    const inSugared = sugared.filter(id => pattern.test(id)), inExplicit = explicit.filter(id => pattern.test(id));
    counts[name] = { sugared: inSugared.length, explicit: inExplicit.length };
    if (!inSugared.length && !exempt.includes(name)) violations.push(`${name} matches no sugared identifier`);
    if (inExplicit.length) violations.push(`${name} matches explicit ${inExplicit.slice(0, 5).join(", ")}`);
  }
  return { counts, violations };
}
