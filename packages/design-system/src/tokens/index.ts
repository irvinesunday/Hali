// Token barrel. Explicit re-exports so consumers can see the surface
// area in one place and treeshaking has named entry points.

export * from "./colors";
export * from "./cssVariables";
export * from "./spacing";
export * from "./radius";
export * from "./typography";

/**
 * Package version sentinel for downstream import-existence checks
 * before the concrete component set lands. Bump when the token set
 * changes in a way that breaks consumers.
 */
export const DesignSystemVersion = "0.1.0";
