const PROXY_CONFIG = [
  {
    context: [
      "/service",
      "/RSTS"
    ],
    "target": "https://10.6.167.116",
    "secure": false, //Don't verify target's cert
    "logLevel": "debug",
    "proxyTimeout": 0,
    "onProxyReq": (proxyReq, req, res) => req.setTimeout(0)
  }
];
  
module.exports = PROXY_CONFIG;