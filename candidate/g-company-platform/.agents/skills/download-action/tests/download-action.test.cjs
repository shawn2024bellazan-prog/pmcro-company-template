const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const os = require("node:os");
const crypto = require("node:crypto");
const {checkUrl,stage,main} = require("../scripts/download-action.cjs");
test("inspect never downloads or activates",async()=>{
 const x=await main([]);
 assert.equal(x.executed,false);assert.equal(x.downloaded,false);assert.equal(x.activated,false);
});
test("local digest verification",async()=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),"pmcro-"));
 try{
   const f=path.join(root,"receipt");fs.writeFileSync(f,"hello");
   const sum=crypto.createHash("sha256").update("hello").digest("hex");
   assert.equal((await main(["--mode","verify","--file",f,"--sha256",sum])).sha256,sum);
   await assert.rejects(main(["--mode","verify","--file",f,"--sha256","0".repeat(64)]),/sha256-mismatch/);
 }finally{fs.rmSync(root,{recursive:true,force:true});}
});
test("reject untrusted sources",()=>{
 for(const u of ["http://github.com/file","file:///etc/passwd","https://evil.example/a",
   "https://github.com.bad.example/a","https://u:p@github.com/a"]){
   assert.throws(()=>checkUrl(u),/unapproved/);
 }
});
test("stage requires approval without network call",async()=>{
 await assert.rejects(stage("https://github.com/a","0".repeat(64),"file.zip",".",false),/manual/);
});
test("filename and staging dir controls",async()=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),"pmcro-"));
 const dir=path.join(root,"candidate-action-downloads");fs.mkdirSync(dir);
 try{
   for(const n of ["..",".","../escape","a/b"])
     await assert.rejects(stage("https://github.com/a","0".repeat(64),n,dir,true),/unsafe/);
   await assert.rejects(stage("https://github.com/a","0".repeat(64),"good.zip",root,true),/staging/);
   assert.deepEqual(fs.readdirSync(dir),[]);
 }finally{fs.rmSync(root,{recursive:true,force:true});}
});