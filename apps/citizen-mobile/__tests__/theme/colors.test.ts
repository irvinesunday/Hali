// apps/citizen-mobile/__tests__/theme/colors.test.ts
//
// Unit tests for getConditionBadgePalette().
// Locks in expected palette mappings and fallback behavior so
// taxonomy additions don't silently break the badge color system.

import { getConditionBadgePalette } from '../../src/theme/colors';

describe('getConditionBadgePalette', () => {
  describe('amber — power / water outages', () => {
    it.each([
      'No power',
      'Power outage',
      'Blackout',
      'No water',
      'Transformer fault',
      'Sewage overflow',
      'Bad smell from drains',
      'Sewage smell',
    ])('maps "%s" → amber', (label) => {
      expect(getConditionBadgePalette(label)).toBe('amber');
    });
  });

  describe('orange — road difficulty', () => {
    it.each([
      'Difficult to pass',
      'Pothole',
      'Lane blocked',
      'Slow moving traffic',
      'Heavy traffic',
      'Road damage',
      'Partially blocked',
      'Road narrowed',
      'Obstruction on road',
      'Sidewalk blocked',
      'Walkway impassable',
      'Crowded street',
      'Uncollected garbage',
      'Dumped waste',
      'Overflowing bin',
    ])('maps "%s" → orange', (label) => {
      expect(getConditionBadgePalette(label)).toBe('orange');
    });
  });

  describe('red — impassable / blocked', () => {
    it.each([
      'Impassable road',
      'Road blocked',
      'Traffic blocked',
      'Accident on N1',
      'Crash reported',
      'Collision ahead',
    ])('maps "%s" → red', (label) => {
      expect(getConditionBadgePalette(label)).toBe('red');
    });
  });

  describe('sky — flooding', () => {
    it.each([
      'Flooding on main road',
      'Water on road',
      'Partially flooded',
      'Water on pavement',
    ])('maps "%s" → sky', (label) => {
      expect(getConditionBadgePalette(label)).toBe('sky');
    });
  });

  describe('violet — noise', () => {
    it.each([
      'Noise disturbance',
      'Noisy construction',
      'Loud music from venue',
    ])('maps "%s" → violet', (label) => {
      expect(getConditionBadgePalette(label)).toBe('violet');
    });
  });

  describe('stone — dust', () => {
    it.each([
      'Dust cloud',
      'Dusty road after grading',
    ])('maps "%s" → stone', (label) => {
      expect(getConditionBadgePalette(label)).toBe('stone');
    });
  });

  describe('slate — darkness / no lighting', () => {
    it.each([
      'Dark area at night',
      'No street lights',
      'No light in underpass',
    ])('maps "%s" → slate', (label) => {
      expect(getConditionBadgePalette(label)).toBe('slate');
    });
  });

  describe('yellow — minor / intermittent conditions', () => {
    it.each([
      'Low pressure water',
      'Weak pressure in taps',
      'Unstable supply',
      'Intermittent outage',
      'Power going on and off',
      'Air pollution visible',
    ])('maps "%s" → yellow', (label) => {
      expect(getConditionBadgePalette(label)).toBe('yellow');
    });
  });

  describe('emerald — restoration', () => {
    it.each([
      'Possible restoration underway',
      'Power restored in area',
      'Recovery in progress',
    ])('maps "%s" → emerald', (label) => {
      expect(getConditionBadgePalette(label)).toBe('emerald');
    });
  });

  describe('muted — fallback', () => {
    it('returns muted for an unrecognised condition', () => {
      expect(getConditionBadgePalette('something completely unknown')).toBe('muted');
    });

    it('returns muted for an empty string', () => {
      expect(getConditionBadgePalette('')).toBe('muted');
    });
  });

  describe('case-insensitivity', () => {
    it('matches regardless of input case', () => {
      expect(getConditionBadgePalette('NO POWER')).toBe('amber');
      expect(getConditionBadgePalette('FLOODING')).toBe('sky');
      expect(getConditionBadgePalette('POTHOLE')).toBe('orange');
    });
  });
});
