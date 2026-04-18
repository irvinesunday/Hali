import { describe, expect, it } from "vitest";
import { formatDurationSeconds } from "./signalFormatting";

describe("formatDurationSeconds", () => {
  it("returns seconds for sub-minute durations", () => {
    expect(formatDurationSeconds(0)).toBe("0s");
    expect(formatDurationSeconds(45)).toBe("45s");
  });

  it("returns minutes for sub-hour durations", () => {
    expect(formatDurationSeconds(60)).toBe("1m");
    expect(formatDurationSeconds(3599)).toBe("59m");
  });

  it("returns hours and optional minutes for sub-day durations", () => {
    expect(formatDurationSeconds(3600)).toBe("1h");
    expect(formatDurationSeconds(3600 + 25 * 60)).toBe("1h 25m");
  });

  it("returns days and optional hours for multi-day durations", () => {
    expect(formatDurationSeconds(86_400)).toBe("1d");
    expect(formatDurationSeconds(86_400 + 5 * 3600)).toBe("1d 5h");
  });

  it("falls back to an em dash for invalid input", () => {
    expect(formatDurationSeconds(Number.NaN)).toBe("—");
    expect(formatDurationSeconds(-1)).toBe("—");
  });
});
