// apps/citizen-mobile/__tests__/utils/localitySelection.test.ts
//
// Direct coverage for the C10 selection-routing branch: when a ward
// is tapped in the locality picker, does it correctly route to
// setActiveLocalityId (followed) vs setActiveLocality (direct)?

import { resolveLocalitySelection } from '../../src/utils/localitySelection';
import type { FollowedLocality, LocalitySummary } from '../../src/types/api';

const southB: FollowedLocality = {
  localityId: 'a',
  wardName: 'South B',
  displayLabel: 'Home',
  cityName: 'Nairobi',
};
const karen: FollowedLocality = {
  localityId: 'b',
  wardName: 'Karen',
  displayLabel: null,
  cityName: 'Nairobi',
};
const followSet: readonly FollowedLocality[] = [southB, karen];

describe('resolveLocalitySelection', () => {
  describe('followed ward', () => {
    it('routes to existingFollow when the ward is in the follow set', () => {
      const ward: LocalitySummary = {
        localityId: 'a',
        wardName: 'South B',
        cityName: 'Nairobi',
      };

      const result = resolveLocalitySelection(ward, followSet);

      expect(result).toEqual({ kind: 'existingFollow', localityId: 'a' });
    });

    it('routes to existingFollow even if the ward summary has a null cityName', () => {
      const ward: LocalitySummary = {
        localityId: 'b',
        wardName: 'Karen',
        cityName: null,
      };

      const result = resolveLocalitySelection(ward, followSet);

      expect(result).toEqual({ kind: 'existingFollow', localityId: 'b' });
    });

    it('matches on localityId only, never on wardName', () => {
      // Two wards with the same name but different IDs must not collide.
      const impostor: LocalitySummary = {
        localityId: 'z',
        wardName: 'South B',
        cityName: 'Nairobi',
      };

      const result = resolveLocalitySelection(impostor, followSet);

      expect(result.kind).toBe('directLocality');
    });
  });

  describe('unfollowed ward', () => {
    it('routes to directLocality when the ward is not in the follow set', () => {
      const ward: LocalitySummary = {
        localityId: 'new-ward-id',
        wardName: 'Umoja I',
        cityName: 'Nairobi',
      };

      const result = resolveLocalitySelection(ward, followSet);

      expect(result.kind).toBe('directLocality');
      if (result.kind === 'directLocality') {
        expect(result.locality.localityId).toBe('new-ward-id');
        expect(result.locality.wardName).toBe('Umoja I');
        expect(result.locality.cityName).toBe('Nairobi');
      }
    });

    it('composes "wardName, cityName" as the displayLabel when city is present', () => {
      const ward: LocalitySummary = {
        localityId: 'x',
        wardName: 'Umoja I',
        cityName: 'Nairobi',
      };

      const result = resolveLocalitySelection(ward, followSet);

      expect(result.kind).toBe('directLocality');
      if (result.kind === 'directLocality') {
        expect(result.locality.displayLabel).toBe('Umoja I, Nairobi');
      }
    });

    it('leaves displayLabel null when cityName is null', () => {
      const ward: LocalitySummary = {
        localityId: 'y',
        wardName: 'Alpha Ward',
        cityName: null,
      };

      const result = resolveLocalitySelection(ward, followSet);

      expect(result.kind).toBe('directLocality');
      if (result.kind === 'directLocality') {
        expect(result.locality.displayLabel).toBeNull();
      }
    });

    it('treats an empty follow set as "everything is unfollowed" (guest path)', () => {
      const ward: LocalitySummary = {
        localityId: 'a',
        wardName: 'South B',
        cityName: 'Nairobi',
      };

      const result = resolveLocalitySelection(ward, []);

      expect(result.kind).toBe('directLocality');
      if (result.kind === 'directLocality') {
        expect(result.locality.displayLabel).toBe('South B, Nairobi');
      }
    });
  });
});
