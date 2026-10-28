import test from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const dll=join(root,'plugins','pmcro-runtime','src','ProjectName.Marketplace.Mcp','bin','Release','net10.0','ProjectName.Marketplace.Mcp.dll');
test('real .NET MCP stdio candidate discovery and deny by default', {timeout:30000}, async()=>{
 const p=spawn('dotnet',[dll],{cwd:root,env:{...process.env,PMCRO_CANDIDATE_ROOT:root,PMCRO_LOCAL_INFERENCE_ENABLED:'false'},stdio:['pipe','pipe','pipe']});
 let nextId=0;const pending=new Map(), logs=[];
 const lines=createInterface({input:p.stdout});
 lines.on('line',line=>{
  let item;try{item=JSON.parse(line)}catch{logs.push(line);return}
  if(item.id!==undefined&&pending.has(item.id)){const {resolve}=pending.get(item.id);pending.delete(item.id);resolve(item)}
 });
 p.stderr.on('data',data=>{if(logs.length<30)logs.push(data.toString())});
 async function request(method,params={}){
  const id=++nextId;
  const value=new Promise((resolve,reject)=>{
   const timer=setTimeout(()=>{pending.delete(id);reject(Error('RPC timeout '+method+' '+logs.join('\n').slice(-1300)))},22000);
   pending.set(id,{resolve:x=>{clearTimeout(timer);resolve(x)}})
  });
  p.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');
  const reply=await value;
  assert.equal(reply.error,undefined,JSON.stringify(reply.error));
  return reply.result;
 }
 try{
  const hello=await request('initialize',{protocolVersion:'2025-06-18',capabilities:{},clientInfo:{name:'pmcro-test',version:'0.1'}});
  assert.ok(hello.protocolVersion);
  p.stdin.write(JSON.stringify({jsonrpc:'2.0',method:'notifications/initialized'})+'\n');
  const tools=await request('tools/list');
  const names=new Set(tools.tools.map(x=>x.name));
  for(const required of ['list_candidate_marketplaces','inspect_candidate_marketplace','list_candidate_marketplace_products','inspect_candidate_marketplace_product','get_pmcro_candidate_status','draft_with_local_ollama']) assert.ok(names.has(required),'Missing '+required+' '+[...names].join(','));
  const catalogue=await request('tools/call',{name:'list_candidate_marketplace_products',arguments:{}});
  const found=JSON.parse(catalogue.content[0].text);
  assert.equal(found.executable,false);
  assert.ok(found.products.includes('download-action.candidate.json'));
  const product=await request('tools/call',{name:'inspect_candidate_marketplace_product',arguments:{productFile:'download-action.candidate.json'}});
  const detail=JSON.parse(product.content[0].text);
  assert.equal(detail.id,'pmcro-orchestrator:download-action');
  assert.equal(detail.activated,false);
  const draft=await request('tools/call',{name:'draft_with_local_ollama',arguments:{messyIntent:'Do not execute anything'}});
  const result=JSON.parse(draft.content[0].text);
  assert.equal(result.reason,'local-inference-opt-in-required');
 }finally{
  p.stdin.end();setTimeout(()=>p.kill(),1200).unref();
 }
});