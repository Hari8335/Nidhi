export function roleHome(roles: readonly string[]): "/admin" | "/dashboard" | null {
  if (roles.length !== 1) return null;
  return roles[0] === "ADMIN" ? "/admin" : roles[0] === "CUSTOMER" ? "/dashboard" : null;
}
