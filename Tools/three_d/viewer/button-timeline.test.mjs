import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {sampleStudy, sequenceDuration} from './button-timeline.js';

const report = JSON.parse(await readFile(new URL('../generated/button-animation-study/study.json', import.meta.url)));
for (const study of report.studies) {
    test(`${study.modelId}: runtime boundaries, completion and mid-clip power overlay`, () => {
        assert.equal(sampleStudy(study, 'Press', .499), 'doorctrl1:2:powered');
        assert.equal(sampleStudy(study, 'Press', .501), 'doorctrl1:0:powered');
        assert.equal(sampleStudy(study, 'Denied', .9, false), 'doorctrl-denied:1:unpowered');
        assert.equal(sampleStudy(study, 'Press', 1.25), 'doorctrl:0:powered');
        assert.equal(sampleStudy(study, 'Press', 1.26, false), 'doorctrl:0:unpowered');
        assert.equal(sampleStudy(study, 'doorctrl-open', 0), 'doorctrl-open:0:powered');
        assert.throws(() => sampleStudy(study, 'missing', 0), /Unknown/);
        assert.throws(() => sampleStudy(study, 'Press', NaN), /Time/);
    });
}
test('full source strip remains distinct from the shorter gameplay clip', () => {
    assert.equal(sequenceDuration(report.studies, 'Press'), 1.25);
    assert.ok(Math.abs(sequenceDuration(report.studies, 'doorctrl1') - 1.3) < 1e-8);
    assert.equal(sequenceDuration(report.studies, 'doorctrl'), 0);
    const red = report.studies.find(s => s.modelId.includes('Red'));
    assert.equal(sampleStudy(red, 'doorctrl1', 1.21), 'doorctrl1:6:powered');
    assert.equal(sampleStudy(red, 'Press', 1.21), 'doorctrl1:3:powered');
});
