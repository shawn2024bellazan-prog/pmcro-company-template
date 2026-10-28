#!/usr/bin/env node
// PMCR-O candidate-only pinned GitHub artifact stager. Does not install or execute.
const fs = require("node:fs");
const fsp = fs.promises;
const crypto = require("node:crypto");
const path = require("node:path");
const https = require("node:https");
const os = require("node:os");
const ALLOWED = new Set(["github.com","api.github.com","raw.githubusercontent.com",
  "objects.githubusercontent.com","release-assets.githubusercontent.com"]);
const SHA = /^[a-fA-F0-9]{64}$/;
const SAFE = /^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$/;
const MAX = 50*1024*1024;
function checkUrl(input) {
  const u = new URL(input);
  if (u.protocol !== "https:" || !ALLOWED.has(u.hostname) || u.username ||
      u.password || (u.port && u.port !== "443") || u.hash || !u.pathname.startsWith("/"))
    throw Error("unapproved-github-https-source");
  return u;
}
async function localDigest(file) {
  const h = crypto.createHash("sha256");
  const stream = fs.createReadStream(file);
  for await (const part of stream) h.update(part);
  return h.digest("hex");
}
async function fetchBytes(url, redirects=0) {
  if(redirects>5) throw Error("too-many-redirects");
  const u = checkUrl(url);
  return new Promise((resolve,reject)=>{
    const req=https.get(u,{headers:{"User-Agent":"PMCR-O-candidate-stage/0.1"},timeout:20000}, res=>{
      if([301,302,303,307,308].includes(res.statusCode)) {
        const dest=new URL(res.headers.location||"",u).href;
        res.resume();checkUrl(dest);
        fetchBytes(dest,redirects+1).then(resolve,reject);return;
      }
      if(res.statusCode!==200) {res.resume();reject(Error("download-http-"+res.statusCode));return;}
      if(Number(res.headers["content-length"]||0)>MAX) {res.destroy();reject(Error("artifact-size-limit"));return;}
      const parts=[];let size=0;
      res.on("data",p=>{size+=p.length;if(size>MAX){res.destroy(Error("artifact-size-limit"));return;}parts.push(p);});
      res.on("end",()=>resolve(Buffer.concat(parts)));
      res.on("error",reject);
    });
    req.on("timeout",()=>req.destroy(Error("download-timeout")));
    req.on("error",reject);
  });
}
async function stage(url,expected,filename,dir,approved) {
  if(!approved) throw Error("manual-staging-approval-required");
  checkUrl(url);
  if(!SHA.test(expected))throw Error("expected-sha256-required");
  if(filename==="."||filename===".."||!SAFE.test(filename))throw Error("unsafe-filename");
  const stat=await fsp.lstat(dir);
  if(!stat.isDirectory()||stat.isSymbolicLink()||path.basename(dir)!=="candidate-action-downloads")
    throw Error("preexisting-staging-directory-required");
  const target=path.join(await fsp.realpath(dir),filename);
  try{await fsp.lstat(target);throw Error("no-overwrite");}catch(e){if(e.code!=="ENOENT")throw e;}
  const bytes=await fetchBytes(url);
  const observed=crypto.createHash("sha256").update(bytes).digest("hex");
  if(observed.toLowerCase()!==expected.toLowerCase())throw Error("sha256-mismatch");
  const tmp=path.join(dir,".candidate-"+crypto.randomUUID()+".part");
  try{
    await fsp.writeFile(tmp,bytes,{flag:"wx",mode:0o600});
    await fsp.link(tmp,target); // atomic no-overwrite in same filesystem
  }finally{await fsp.rm(tmp,{force:true});}
  return {status:"staged-only",sha256:observed,bytes:bytes.length,path:target,
          installed:false,executed:false};
}
async function main(argv) {
  const a={mode:"inspect"};
  for(let i=0;i<argv.length;i++){
    const k=argv[i];
    if(k==="--approve-stage"){a.approved=true;continue;}
    if(!k.startsWith("--")||i+1>=argv.length)throw Error("invalid-arguments");
    a[k.slice(2)]=argv[++i];
  }
  if(a.mode==="inspect")return {status:"candidate-only",downloaded:false,installed:false,
                                      registered:false,activated:false,executed:false};
  if(a.mode==="verify") {
    if(!a.file||!SHA.test(a.sha256||""))throw Error("file-and-sha256-required");
    const observed=await localDigest(a.file);
    if(observed.toLowerCase()!==a.sha256.toLowerCase())throw Error("sha256-mismatch");
    return {status:"verified-local-only",sha256:observed};
  }
  if(a.mode!=="stage")throw Error("unsupported-mode");
  return stage(a.url,a.sha256,a.filename,a["stage-dir"],a.approved===true);
}
if(require.main===module){main(process.argv.slice(2)).then(x=>console.log(JSON.stringify(x)),
  e=>{console.error(JSON.stringify({status:"blocked",reason:e.message}));process.exitCode=2;});}
module.exports={checkUrl,stage,localDigest,main};