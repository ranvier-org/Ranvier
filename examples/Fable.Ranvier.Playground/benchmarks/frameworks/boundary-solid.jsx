import { createMemo, createSignal, flush, Loading, Show } from "solid-js";
import { render } from "@solidjs/web";

// Rows reading one async value under a Loading boundary; resolves the value and waits for the reveal.
export async function mountReveal(host, width) {
  let resolve;
  const promise = new Promise(settle => { resolve = settle; });
  const dispose = render(() => {
    const value = createMemo(() => promise);
    return <Loading fallback={<span>loading</span>}>
      {Array.from({ length: width }, (_, i) => <div class="row">{value() + i}</div>)}
    </Loading>;
  }, host);
  flush();
  resolve("ready");
  for (let turn = 0; turn < 20 && host.querySelectorAll(".row").length !== width; turn++) {
    await Promise.resolve();
    flush();
  }
  return dispose;
}

// A Loading boundary mounted by a Show switch over an already resolved value; the boundary mounts inside a flush.
export async function mountSwitchReveal(host, width) {
  const promise = Promise.resolve("ready");
  const [arm, setArm] = createSignal(false);
  const dispose = render(() => {
    const value = createMemo(() => promise);
    return <Show when={arm()}>
      <Loading fallback={<span>loading</span>}>
        {Array.from({ length: width }, (_, i) => <div class="row">{value() + i}</div>)}
      </Loading>
    </Show>;
  }, host);
  flush();
  setArm(true);
  flush();
  for (let turn = 0; turn < 20 && host.querySelectorAll(".row").length !== width; turn++) {
    await Promise.resolve();
    flush();
  }
  return dispose;
}
