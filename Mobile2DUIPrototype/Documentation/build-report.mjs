import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const directory=path.dirname(fileURLToPath(import.meta.url));
const root=path.dirname(directory);
const template=fs.readFileSync(path.join(directory,'implementation-report.template.html'),'utf8');
const report=template.replace(/\{\{IMG:([^}]+)\}\}/g,(_,name)=>{
  const data=fs.readFileSync(path.join(root,'Screenshots',name));
  return 'data:image/png;base64,'+data.toString('base64');
});
const output=path.join(directory,'모바일_UI_구현_보고서.html');
fs.writeFileSync(output,report,'utf8');
console.log(output);
console.log('Sections: '+(report.match(/<section /g)||[]).length);
console.log('Embedded images: '+(report.match(/data:image\/png;base64/g)||[]).length);
console.log('Unresolved placeholders: '+(report.match(/\{\{IMG:/g)||[]).length);
