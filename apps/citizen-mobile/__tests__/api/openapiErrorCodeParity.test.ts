// apps/citizen-mobile/__tests__/api/openapiErrorCodeParity.test.ts
//
// Drift guard: the mobile `ERROR_CODES` mirror in
// `apps/citizen-mobile/src/types/api.ts` MUST equal the canonical
// `ErrorCode` enum published in `02_openapi.yaml#/components/schemas/ErrorCode`.
//
// The server-side catalog (`src/Hali.Application/Errors/ErrorCodes.cs`) is
// the source of truth; the OpenAPI enum is the wire-visible subset; this
// mirror is the mobile-visible subset. By construction all three should
// be identical for wire-visible codes — the matching backend test
// (`ErrorCodeCatalogTests`) enforces server↔OpenAPI parity, and this
// test enforces mobile↔OpenAPI parity, transitively closing the loop.
//
// Implementation note: this test reads `02_openapi.yaml` from the repo
// root with a tiny line-based scanner instead of pulling in a full YAML
// parser. The relevant block is a single named enum with a fixed shape,
// so a 15-line scanner is the lowest-friction approach.

import * as fs from 'fs';
import * as path from 'path';
import { ERROR_CODES } from '../../src/types/api';

/**
 * Extract the string values listed under the named `ErrorCode:` schema
 * in `02_openapi.yaml`. The block looks like:
 *
 *     ErrorCode:
 *       type: string
 *       description: |
 *         …
 *       enum:
 *         - account.not_found
 *         - auth.forbidden
 *         …
 *
 *     ErrorResponse:
 *
 * Scanning rules:
 *   1. Find the line `    ErrorCode:` (4-space indent under `schemas:`).
 *   2. Then find the `enum:` line under it.
 *   3. Collect each subsequent `- value` line until indentation drops
 *      back to the parent schema column (next sibling schema starts).
 */
function readOpenApiErrorCodes(): string[] {
  const yamlPath = path.resolve(__dirname, '../../../../02_openapi.yaml');
  const lines = fs.readFileSync(yamlPath, 'utf8').split('\n');

  const ERROR_CODE_HEADER = /^    ErrorCode:\s*$/;
  const ENUM_LINE = /^      enum:\s*$/;
  const ENUM_ITEM = /^        - (\S+)\s*$/;
  const NEXT_SIBLING_SCHEMA = /^    \S/;

  let i = 0;
  while (i < lines.length && !ERROR_CODE_HEADER.test(lines[i])) i++;
  if (i === lines.length) {
    throw new Error(
      'Could not locate `    ErrorCode:` block in 02_openapi.yaml — ' +
        'either the schema was renamed or its indentation changed. ' +
        'Update the scanner in openapiErrorCodeParity.test.ts.',
    );
  }
  i++;

  while (i < lines.length && !ENUM_LINE.test(lines[i])) {
    if (NEXT_SIBLING_SCHEMA.test(lines[i])) {
      throw new Error(
        'ErrorCode schema has no `enum:` block — OpenAPI structure changed.',
      );
    }
    i++;
  }
  i++;

  const codes: string[] = [];
  while (i < lines.length) {
    const m = ENUM_ITEM.exec(lines[i]);
    if (m) {
      codes.push(m[1]);
      i++;
      continue;
    }
    if (lines[i].trim() === '') {
      i++;
      continue;
    }
    // Reached the next sibling schema (or any line at parent indent).
    break;
  }

  if (codes.length === 0) {
    throw new Error('OpenAPI ErrorCode enum scanner returned zero entries.');
  }
  return codes;
}

describe('OpenAPI ↔ mobile ErrorCode parity', () => {
  // Use plain `string[]` for both sides — Set comparison is symmetric and
  // ErrorCode's literal-union type would otherwise reject the OpenAPI
  // strings (which are typed `string`, not `ErrorCode`, by construction).
  const openApiCodes: string[] = readOpenApiErrorCodes();
  const mobileCodes: string[] = Object.values(ERROR_CODES);

  it('every OpenAPI ErrorCode value is mirrored in mobile ERROR_CODES', () => {
    const mobileSet = new Set<string>(mobileCodes);
    const missing = openApiCodes.filter((c) => !mobileSet.has(c));
    expect(missing).toEqual([]);
  });

  it('every mobile ERROR_CODES value exists in the OpenAPI ErrorCode enum', () => {
    const openApiSet = new Set<string>(openApiCodes);
    const extra = mobileCodes.filter((c) => !openApiSet.has(c));
    expect(extra).toEqual([]);
  });

  it('mobile mirror is exactly the same size as the OpenAPI enum', () => {
    expect(mobileCodes.length).toBe(openApiCodes.length);
  });
});
