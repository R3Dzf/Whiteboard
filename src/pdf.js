/* Tiny one-page PDF writer for an offline, dependency-free image export.
   The board is rendered by the browser to JPEG and embedded in a valid PDF. */
(function(root){
'use strict';
const encoder=new TextEncoder();
const utf8=x=>encoder.encode(x);
function imagePdf(jpeg,width,height){
  if(!(jpeg instanceof Uint8Array)||jpeg.length<4||jpeg[0]!==255||jpeg[1]!==216)throw Error('Expected JPEG bytes');
  if(!Number.isFinite(width)||!Number.isFinite(height)||width<1||height<1||width>32000||height>32000)throw Error('Invalid image dimensions');
  const landscape=width>=height,pgW=landscape?841.89:595.28,pgH=landscape?595.28:841.89;
  const margin=24,scale=Math.min((pgW-margin*2)/width,(pgH-margin*2)/height);
  const w=width*scale,h=height*scale,x=(pgW-w)/2,y=(pgH-h)/2;
  const command=`q\n${w.toFixed(3)} 0 0 ${h.toFixed(3)} ${x.toFixed(3)} ${y.toFixed(3)} cm\n/Im0 Do\nQ\n`;
  const objs=[
    utf8('<< /Type /Catalog /Pages 2 0 R >>'),
    utf8('<< /Type /Pages /Kids [3 0 R] /Count 1 >>'),
    utf8(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pgW} ${pgH}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>`),
    {header:utf8(`<< /Type /XObject /Subtype /Image /Width ${width} /Height ${height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`),data:jpeg},
    {header:utf8(`<< /Length ${encoder.encode(command).length} >>\nstream\n`),data:utf8(command)}
  ];
  const chunks=[],offsets=[0];let size=0;
  function append(buf){chunks.push(buf);size+=buf.length;}
  append(utf8('%PDF-1.4\n%\xE2\xE3\xCF\xD3\n'));
  objs.forEach((item,i)=>{
    offsets.push(size);append(utf8(`${i+1} 0 obj\n`));
    if(item instanceof Uint8Array){append(item);append(utf8('\n'))}
    else{append(item.header);append(item.data);append(utf8('\nendstream\n'))}
    append(utf8('endobj\n'));
  });
  const start=size;
  append(utf8(`xref\n0 ${objs.length+1}\n0000000000 65535 f \n`));
  offsets.slice(1).forEach(n=>append(utf8(String(n).padStart(10,'0')+' 00000 n \n')));
  append(utf8(`trailer\n<< /Size ${objs.length+1} /Root 1 0 R >>\nstartxref\n${start}\n%%EOF\n`));
  const output=new Uint8Array(size);let at=0;for(const chunk of chunks){output.set(chunk,at);at+=chunk.length;}
  return output;
}
const api={imagePdf};if(typeof module!=='undefined'&&module.exports)module.exports=api;
root.WhiteboardPdf=api;
})(typeof window==='undefined'?globalThis:window);
