import base from './next.base.mjs';
// Only the isolated demo image permits its explicit IPv4 loopback dev origin.
export default { ...base, allowedDevOrigins: ['127.0.0.1'] };
