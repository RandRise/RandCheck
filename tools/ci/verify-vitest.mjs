import { readFileSync, writeFileSync } from 'node:fs';
const result = JSON.parse(readFileSync('/results/web-tests.json', 'utf8'));
if (!result.success || result.numTotalTests < 1 || result.numPassedTests !== result.numTotalTests || result.numPendingTests || result.numTodoTests) {
  throw new Error('Browser test results must contain passing tests with no skipped/todo cases.');
}
writeFileSync('/results/web-counts.json', JSON.stringify({passed: result.numPassedTests}) + '\n');
