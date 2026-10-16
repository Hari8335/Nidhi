export function roleHome(roles: readonly string[]): "/admin" | "/dashboard" | null {
  if (roles.length !== 1) return null;
  return roles[0] === "ADMIN" ? "/admin" : roles[0] === "CUSTOMER" ? "/dashboard" : null;
}

// Only known same-origin destinations within the resolved role can be resumed.
export function signInDestination(roles: readonly string[], returnTo: string | null) {
  const home = roleHome(roles);
  if (!home || !returnTo) return home;
  const customer = /^\/(dashboard|wallet|gold|transactions(?:\/[0-9a-f-]{36})?|goals|profile)$/i;
  const admin = /^\/admin(?:\/(customers|transactions)(?:\/[0-9a-f-]{36})?|\/(ledger|prices|audit-events))?$/i;
  return (home === "/admin" ? admin : customer).test(returnTo) ? returnTo : home;
}
