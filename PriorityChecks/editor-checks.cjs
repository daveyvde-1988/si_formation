const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert/strict');
const base=path.resolve(__dirname,'../silica-formation-editor/Silica-FormationTool');
const context=vm.createContext({console});
for(const name of ['game-icons.js','core.js','shapes.js','footprints.js'])vm.runInContext(fs.readFileSync(path.join(base,name),'utf8'),context,{filename:name});
const C=context.FormationCore,S=context.FormationShapes,F=context.FormationFootprints;
let checks=0;function check(value,label){assert.ok(value,label);console.log('PASS '+label);checks++;}
for(const [category,names] of Object.entries(C.categoryOverrides))for(const name of names){const entry=Object.entries(C.metadata).find(([key])=>key.endsWith('|'+name));assert.equal(entry?.[1].type,category,name);}
check(!C.unitTypes.includes('Harvester'),'all requested categories and no selectable Harvester');
let f=C.newFormation();f.slots=[{id:C.id(),x:0,z:3,role:'top',preference:0,preferredUnits:['Hover Tank'],preferredTypes:['Tank']}];
let result=C.autoBackups(f);
check(result.added.length===5&&f.slots.find(s=>s.preference===1).z===2&&f.slots.find(s=>s.preference===2).z===1,'yellow/orange behind green; red beside every tier');
check(f.slots.filter(s=>s.preference===1||s.preference===2).every(s=>s.preferredUnits[0]==='Hover Tank'&&s.preferredTypes[0]==='Tank'),'backup unit and category preferences copied');
check(f.slots.filter(s=>s.preference===4).every(s=>!s.preferredUnits.length&&!s.preferredTypes.length),'red backups remain unrestricted');
f=C.newFormation();f.directionDegrees=90;f.slots=[{id:C.id(),x:0,z:0,role:'top',preferredUnits:[]}];C.autoBackups(f);
check(f.slots.find(s=>s.preference===1).x===-1&&f.slots.find(s=>s.preference===2).x===-2,'backup direction follows rotated arrow');
f=C.newFormation();f.slots=[{id:C.id(),x:-25,z:-25,role:'top',preferredUnits:[]}];result=C.autoBackups(f);
check(result.skipped>=2&&f.slots.every(s=>s.x>=-25&&s.x<=24&&s.z>=-25&&s.z<=24)&&new Set(f.slots.map(s=>s.x+','+s.z)).size===f.slots.length,'boundary handling avoids clamped/duplicate positions');
for(const type of S.types.filter(S.canFill))for(const count of [1,8,19,20]){
 const r=S.generate({type,count,tactical:true,size:6,origin:{x:.5,z:-.5}});
 assert.ok(r.points.length<=count);
 assert.ok(r.points.every(p=>r.points.some(q=>Math.abs(q.x-(1-p.x))<1e-8&&q.z===p.z)),type+' mirror symmetry');
 assert.ok(r.points.every((p,i)=>r.points.every((q,j)=>i===j||Math.hypot(p.x-q.x,p.z-q.z)>=.94-1e-8)),type+' non-overlap');
}
check(true,'Tactical fill symmetry and spacing across every area shape, odd/even counts');
F.load({format:'silica-unit-footprints',version:1,units:{'Sol|Hover Tank':{length:8,width:4},'Sol|Railgun Tank':{length:12,width:3}}});
let fp=F.footprint({preferredUnits:['Hover Tank']},'Sol');
check(fp.length/4===2&&fp.width/4===1&&fp.length/8===1&&fp.width/8===.5&&!fp.generic,'8 × 4 m physical footprint scales at 4 and 8 metres per position');
fp=F.footprint({preferredTypes:['Tank']},'Sol');check(fp.length===12&&fp.width===4&&!fp.generic,'category footprint uses conservative width/length envelope');
check(F.footprint({preferredUnits:[]},'Sol').generic&&F.footprint({preferredUnits:['AA Truck']},'Sol').generic,'unrestricted and missing measurements clearly generic');
assert.throws(()=>F.load({format:'silica-unit-footprints',version:1,units:{'Sol|Hover Tank':{length:NaN,width:4}}}));
check(true,'invalid footprint data rejected');
f=C.newFormation();f.slots=[{id:C.id(),x:0,z:3,role:'top',preference:0,preferredUnits:['Hover Tank'],preferredTypes:['Tank']}];C.autoBackups(f);
const imported=C.importDocument(C.exportDocument([f]))[0];
check(imported.slots.length===f.slots.length&&imported.slots.filter(s=>s.preferredTypes.includes('Tank')).length===3,'backup priorities and category preferences round-trip through JSON');
for(const name of ['core.js','shapes.js','footprints.js','editor.js','zip.js'])new vm.Script(fs.readFileSync(path.join(base,name),'utf8'),{filename:name});
const html=fs.readFileSync(path.join(base,'index.html'),'utf8'),editor=fs.readFileSync(path.join(base,'editor.js'),'utf8');
check(!html.includes('exportImages')&&!editor.includes('includeImages')&&!html.includes('Tactical shape'),'removed PNG option and renamed Tactical fill');
check(editor.includes('.side > .section, .inspector > .section:not(#details)'),'both sidebars wrapped in collapsible sections');
console.log(checks+' editor checks passed (no browser or live game).');
