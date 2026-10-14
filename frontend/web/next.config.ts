import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    const origin = (process.env.API_BASE_URL || "http://localhost:5050").replace(/\/$/, "");
    return [{ source: "/api/:path*", destination: `${origin}/api/:path*` }];
  },
  async headers() {
    return [{ source: "/:path*", headers: [{ key: "Referrer-Policy", value: "no-referrer" }, { key: "X-Content-Type-Options", value: "nosniff" }] }];
  },
};

export default nextConfig;
