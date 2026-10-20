// Runs the "Check answer" Code node from the assistant workflow on sample answers (#29). Node 18+, no packages.
//   node n8n/eval/check-answer.test.mjs        (also in CI)
import { readFileSync } from "node:fs";
import assert from "node:assert/strict";

const workflow = JSON.parse(readFileSync(new URL("../workflows/loopcasts-assistant.json", import.meta.url)));
const code = workflow.nodes.find(n => n.name === "Check answer").parameters.jsCode;
const check = output => new Function("$json", "$env", code)({ output }, { LOOPCASTS_PUBLIC_URL: "https://shop.test/" }).json.output;

// bare paths and relative markdown links → full links
assert.equal(check("See /Pricing."), "See [Pricing](https://shop.test/Pricing).");
assert.equal(check("Order here (/Order?plan=yearly)"), "Order here ([Order](https://shop.test/Order?plan=yearly))");
assert.equal(check("Go to /Identity/Account/ForgotPassword"), "Go to [ForgotPassword](https://shop.test/Identity/Account/ForgotPassword)");
assert.equal(check("[courses](/Courses)"), "[courses](https://shop.test/Courses)");
assert.equal(check("[order](http://host.docker.internal:5180/Order?plan=team)"), "[order](https://shop.test/Order?plan=team)");
// a link to another host is invented: known page → our base URL, anything else → only the text
assert.equal(check("[Pricing page](https://loopcasts.example/pricing)"), "[Pricing page](https://shop.test/Pricing)");
assert.equal(check("[order](https://loopcasts.com/order?plan=yearly)"), "[order](https://shop.test/Order?plan=yearly)");
assert.equal(check("See [our blog](https://loopcasts.example/blog)."), "See our blog.");
// good answers stay as they are
const good = "The yearly plan is €149 per year, excl. VAT: [order it here](https://shop.test/Order?plan=yearly).";
assert.equal(check(good), good);
assert.equal(check("We don't sell 1/2 courses."), "We don't sell 1/2 courses.");
// JSON or code → friendly sentence
for (const bad of ['[{"code": "yearly", "price": 149}]', '{"plans": []}', "Here:\n```json\n[]\n```", 'Plans: "monthly": 15'])
  assert.match(check(bad), /^Sorry, I couldn't answer that properly\. See \[courses\]\(https:\/\/shop\.test\/Courses\)/);
// model failed → friendly message, never empty
for (const failed of [undefined, "", "  "])
  assert.match(check(failed), /^Sorry, the assistant isn't available right now\./);

console.log("check-answer: all good");
