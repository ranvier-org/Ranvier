import { createSignal, onCleanup } from "solid-js";
import { render } from "solid-js/web";

// Mounts an input whose value is bound to a signal written by its own input handler, and returns its disposal.
export function caret(host) {
  const [source, setSource] = createSignal("");
  return render(() => <input value={source()} onInput={event => setSource(event.currentTarget.value)} />, host);
}

// Mounts four rows whose third row fails during mount, by its property setter or by its title reader.
export function failure(host, kind) {
  const [source, setSource] = createSignal("a");
  let cleaned = 0, reads = 0, threw = false, message = "", dispose;
  const title = index => {
    reads++;
    if (kind === "reader" && index === 3) throw Error("reader");
    return source();
  };
  const Row = props => {
    onCleanup(() => cleaned++);
    return <div title={title(props.index)} prop:explode={kind === "setter" && props.index === 3 ? "boom" : "ok"} />;
  };
  try {
    dispose = render(() => <section>{[1, 2, 3, 4].map(index => <Row index={index} />)}</section>, host);
  } catch (error) {
    threw = true;
    message = String(error && error.message);
  }
  return {
    threw, message,
    update() { const before = reads; setSource("b"); return reads - before; },
    dispose() { dispose?.(); },
    cleaned: () => cleaned
  };
}
