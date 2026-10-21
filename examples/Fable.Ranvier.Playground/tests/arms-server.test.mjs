import test from "node:test";
import { cases } from "../generated-tests/experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.js";

for (const [name, check] of cases) test(name, () => check());
