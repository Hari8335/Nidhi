export type Role = "CUSTOMER" | "ADMIN";
export type CurrentUser = {
  userId: string;
  email: string;
  displayName: string | null;
  isEmailVerified: boolean;
  roles: string[];
};
export type LoginResponse = { userId: string; email: string; role: Role; isEmailVerified: boolean };
export type RegistrationResponse = { customerId: string; email: string; displayName: string | null; isEmailVerified: boolean; message: string };
export type MessageResponse = { message: string };
