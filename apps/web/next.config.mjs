/** @type {import('next').NextConfig} */
const nextConfig = { output: "standalone", distDir: process.env.NEXT_DIST_DIR ?? ".next",
  async headers() { return [{ source: "/auth/callback", headers: [
    { key: "Cache-Control", value: "no-store" }, { key: "Referrer-Policy", value: "no-referrer" }
  ] }]; }
};
export default nextConfig;
