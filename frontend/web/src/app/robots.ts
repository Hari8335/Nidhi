import type { MetadataRoute } from "next";

export default function robots(): MetadataRoute.Robots {
  return { rules: { userAgent: "*", allow: "/", disallow: ["/api/", "/admin", "/dashboard", "/wallet", "/gold", "/transactions", "/goals", "/profile", "/login", "/register", "/verify-email", "/forgot-password", "/reset-password"] } };
}
