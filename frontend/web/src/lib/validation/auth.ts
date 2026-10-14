export const passwordGuidance = "Use 12–128 characters, with uppercase and lowercase letters, a number, and a symbol.";
export function passwordError(password: string): string | undefined {
  if (password.length < 12 || password.length > 128 || !/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password) || !/[^a-zA-Z0-9]/.test(password)) return passwordGuidance;
}
export function confirmationError(password: string, confirmation: string): string | undefined {
  if (password !== confirmation) return "Passwords do not match.";
}
