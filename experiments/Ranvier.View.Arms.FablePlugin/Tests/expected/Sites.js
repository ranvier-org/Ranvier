
import { Signal$1__get_Value } from "./src/Ranvier/Core.js";
import { Hooks, Element$2__Yield_Z5833CC7, Element$2__Yield_Z721C83C5, Element$2__Yield_Z38B9F583, Element$2__Delay_Z3DB59124, Element$2__Run_Z2BE2850A, Component_template } from "./Ranvier.View.Portable/TemplateSugar.js";
import { section$1_$ctor_74B6D47, i$1_$ctor_74B6D47, pre$1_$ctor_Z11E5C9E0, b$1_$ctor_74B6D47, span$1_$ctor_659A19AB, div$1_$ctor_5298DB1C } from "./Ranvier.View.Portable/Generated/HtmlTemplateSugar.js";
import { Arms_Match_Z9579909 } from "./Ranvier.View.Arms/Arms.js";
import { Exception, int32ToString } from "./fable_modules/fable-library-js/Util.js";
import { HookBuilder$1__exit_Z20F364BA } from "./Ranvier.View.Portable/Template.js";

export function read(state) {
    return Signal$1__get_Value(state);
}

export const rich = Component_template((_arg, state) => state, (() => {
    const builder$0040 = div$1_$ctor_5298DB1C();
    return Element$2__Run_Z2BE2850A(builder$0040, Element$2__Delay_Z3DB59124(builder$0040, () => Element$2__Yield_Z38B9F583(builder$0040, Arms_Match_Z9579909(read, null, (armsValues_S1) => {
        let builder$0040_1, builder$0040_2, builder$0040_3, builder$0040_4, builder$0040_5;
        return [(_arg_1) => {
            let matchResult, percent, count_1, text_1, count_2, text_2;
            switch (_arg_1.tag) {
                case 1: {
                    if (_arg_1.fields[0] === 0) {
                        matchResult = 0;
                    }
                    else {
                        matchResult = 1;
                        percent = _arg_1.fields[0];
                    }
                    break;
                }
                case 2: {
                    if (_arg_1.fields[1] === 0) {
                        matchResult = 2;
                    }
                    else if (_arg_1.fields[1] > 99) {
                        matchResult = 3;
                        count_1 = _arg_1.fields[1];
                        text_1 = _arg_1.fields[0];
                    }
                    else {
                        matchResult = 4;
                        count_2 = _arg_1.fields[1];
                        text_2 = _arg_1.fields[0];
                    }
                    break;
                }
                default:
                    matchResult = 0;
            }
            switch (matchResult) {
                case 0:
                    return [0, [_arg_1]];
                case 1:
                    return [1, [percent, _arg_1]];
                case 2:
                    return [2, [_arg_1]];
                case 3:
                    return [3, [count_1, text_1, _arg_1]];
                default:
                    return [4, [count_2, text_2, _arg_1]];
            }
        }, [(builder$0040_1 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_1, Element$2__Delay_Z3DB59124(builder$0040_1, () => Element$2__Yield_Z721C83C5(builder$0040_1, "Not started")))), (builder$0040_2 = b$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_2, Element$2__Delay_Z3DB59124(builder$0040_2, () => Element$2__Yield_Z5833CC7(builder$0040_2, (_arg_2) => {
            const armsRead_S1 = armsValues_S1(_arg_2, 1);
            return int32ToString(armsRead_S1[0]);
        })))), (builder$0040_3 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_3, Element$2__Delay_Z3DB59124(builder$0040_3, () => Element$2__Yield_Z721C83C5(builder$0040_3, "Empty")))), (builder$0040_4 = b$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_4, Element$2__Delay_Z3DB59124(builder$0040_4, () => Element$2__Yield_Z5833CC7(builder$0040_4, (_arg_3) => {
            const armsRead_S1 = armsValues_S1(_arg_3, 3);
            return armsRead_S1[1];
        })))), (builder$0040_5 = pre$1_$ctor_Z11E5C9E0(), Element$2__Run_Z2BE2850A(builder$0040_5, Element$2__Delay_Z3DB59124(builder$0040_5, () => Element$2__Yield_Z5833CC7(builder$0040_5, (_arg_4) => {
            const armsRead_S1 = armsValues_S1(_arg_4, 4);
            const count_2_S1 = armsRead_S1[0] | 0;
            return armsRead_S1[1] + int32ToString(count_2_S1);
        }))))]];
    }))));
})());

export const incomplete = Component_template((_arg, state) => state, (() => {
    const builder$0040 = div$1_$ctor_5298DB1C();
    return Element$2__Run_Z2BE2850A(builder$0040, Element$2__Delay_Z3DB59124(builder$0040, () => Element$2__Yield_Z38B9F583(builder$0040, Arms_Match_Z9579909(read, null, (armsValues_S2) => {
        let builder$0040_1, builder$0040_2;
        return [(_arg_1) => {
            switch (_arg_1.tag) {
                case 0:
                    return [0, [_arg_1]];
                case 1:
                    return [1, [_arg_1.fields[0], _arg_1]];
                default:
                    throw new Exception("Match failure: Fixtures.Types.Load");
            }
        }, [(builder$0040_1 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_1, Element$2__Delay_Z3DB59124(builder$0040_1, () => Element$2__Yield_Z721C83C5(builder$0040_1, "Not started")))), (builder$0040_2 = b$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_2, Element$2__Delay_Z3DB59124(builder$0040_2, () => Element$2__Yield_Z5833CC7(builder$0040_2, (_arg_2) => {
            const armsRead_S2 = armsValues_S2(_arg_2, 1);
            return int32ToString(armsRead_S2[0]);
        }))))]];
    }))));
})());

export const held = Component_template((_arg, state) => state, (() => {
    const builder$0040 = div$1_$ctor_5298DB1C();
    return Element$2__Run_Z2BE2850A(builder$0040, Element$2__Delay_Z3DB59124(builder$0040, () => Element$2__Yield_Z38B9F583(builder$0040, Arms_Match_Z9579909(read, null, (armsValues_S3) => {
        let builder$0040_1, builder$0040_2;
        return [(_arg_1) => ((_arg_1.tag === 2) ? [0, [_arg_1.fields[0], _arg_1]] : [1, [_arg_1]]), [(builder$0040_1 = span$1_$ctor_659A19AB(HookBuilder$1__exit_Z20F364BA(Hooks(), (_arg_2, _arg_3, finish) => {
            const armsRead_S3 = armsValues_S3(_arg_2, 0);
            if (armsRead_S3[0] === "") {
                finish();
            }
            return () => {
            };
        })), Element$2__Run_Z2BE2850A(builder$0040_1, Element$2__Delay_Z3DB59124(builder$0040_1, () => Element$2__Yield_Z721C83C5(builder$0040_1, "held")))), (builder$0040_2 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_2, Element$2__Delay_Z3DB59124(builder$0040_2, () => Element$2__Yield_Z721C83C5(builder$0040_2, "Other"))))]];
    }))));
})());

export const bodies = Component_template((_arg, state) => state, (() => {
    const builder$0040 = div$1_$ctor_5298DB1C();
    return Element$2__Run_Z2BE2850A(builder$0040, Element$2__Delay_Z3DB59124(builder$0040, () => Element$2__Yield_Z38B9F583(builder$0040, Arms_Match_Z9579909(read, null, (armsValues_S4) => {
        let builder$0040_1, builder$0040_2, builder$0040_3, builder$0040_4, builder$0040_5;
        return [(_arg_1) => ((_arg_1.tag === 2) ? ((_arg_1.fields[1] > 100) ? ((_arg_1.fields[1] > 1000) ? [0, [_arg_1.fields[1], _arg_1]] : [1, [_arg_1.fields[1], _arg_1]]) : ((_arg_1.fields[1] === 0) ? [2, [_arg_1.fields[0], _arg_1.fields[0] + "!", _arg_1]] : [3, [_arg_1.fields[0], _arg_1]])) : [4, [_arg_1]]), [(builder$0040_1 = b$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_1, Element$2__Delay_Z3DB59124(builder$0040_1, () => Element$2__Yield_Z721C83C5(builder$0040_1, "huge")))), (builder$0040_2 = i$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_2, Element$2__Delay_Z3DB59124(builder$0040_2, () => Element$2__Yield_Z721C83C5(builder$0040_2, "big")))), (builder$0040_3 = pre$1_$ctor_Z11E5C9E0(), Element$2__Run_Z2BE2850A(builder$0040_3, Element$2__Delay_Z3DB59124(builder$0040_3, () => Element$2__Yield_Z5833CC7(builder$0040_3, (_arg_2) => {
            const armsRead_S4 = armsValues_S4(_arg_2, 2);
            return armsRead_S4[1];
        })))), (builder$0040_4 = pre$1_$ctor_Z11E5C9E0(), Element$2__Run_Z2BE2850A(builder$0040_4, Element$2__Delay_Z3DB59124(builder$0040_4, () => Element$2__Yield_Z5833CC7(builder$0040_4, (_arg_3) => {
            const armsRead_S4 = armsValues_S4(_arg_3, 3);
            return armsRead_S4[0];
        })))), (builder$0040_5 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_5, Element$2__Delay_Z3DB59124(builder$0040_5, () => Element$2__Yield_Z721C83C5(builder$0040_5, "Other"))))]];
    }))));
})());

export const nested = Component_template((_arg, panel) => panel, (() => {
    const builder$0040 = div$1_$ctor_5298DB1C();
    return Element$2__Run_Z2BE2850A(builder$0040, Element$2__Delay_Z3DB59124(builder$0040, () => Element$2__Yield_Z38B9F583(builder$0040, Arms_Match_Z9579909((panel_1) => Signal$1__get_Value(panel_1.State), null, (armsValues_S5) => {
        let builder$0040_1, builder$0040_4;
        return [(_arg_1) => ((_arg_1.tag === 2) ? [0, [_arg_1.fields[0], _arg_1]] : [1, [_arg_1]]), [(builder$0040_1 = section$1_$ctor_74B6D47(), Element$2__Run_Z2BE2850A(builder$0040_1, Element$2__Delay_Z3DB59124(builder$0040_1, () => Element$2__Yield_Z38B9F583(builder$0040_1, Arms_Match_Z9579909((panel_2) => Signal$1__get_Value(panel_2.Open), null, (armsValues_S6) => {
            let builder$0040_2, builder$0040_3;
            return [(_arg_2) => (_arg_2 ? [0, [_arg_2]] : [1, [_arg_2]]), [(builder$0040_2 = pre$1_$ctor_Z11E5C9E0(), Element$2__Run_Z2BE2850A(builder$0040_2, Element$2__Delay_Z3DB59124(builder$0040_2, () => Element$2__Yield_Z5833CC7(builder$0040_2, (_arg_3) => {
                const armsRead_S5 = armsValues_S5(_arg_3, 0);
                return armsRead_S5[0];
            })))), (builder$0040_3 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_3, Element$2__Delay_Z3DB59124(builder$0040_3, () => Element$2__Yield_Z721C83C5(builder$0040_3, "closed"))))]];
        }))))), (builder$0040_4 = span$1_$ctor_659A19AB(), Element$2__Run_Z2BE2850A(builder$0040_4, Element$2__Delay_Z3DB59124(builder$0040_4, () => Element$2__Yield_Z721C83C5(builder$0040_4, "Other"))))]];
    }))));
})());

//# sourceMappingURL=Sites.js.map
