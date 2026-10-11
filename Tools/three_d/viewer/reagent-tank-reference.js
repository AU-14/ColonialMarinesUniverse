// Original four-layer source order. This helper draws an offline snapshot, not solution logic.
export function tankReferenceCanvas(images,{state='tn_color-1',visible=false,color='#FFFFFF',alpha=1,spriteTint='#FFFFFF',spriteAlpha=1}) {
    const buffer=document.createElement('canvas');buffer.width=32;buffer.height=32;const ctx=buffer.getContext('2d');
    ctx.drawImage(images.tank_normal,0,0);ctx.drawImage(images['tn_color-1'],0,0);
    function tint(context,rgb,opacity){
        const pixels=context.getImageData(0,0,32,32),channels=[1,3,5].map(i=>parseInt(rgb.slice(i,i+2),16)/255);
        for(let i=0;i<pixels.data.length;i+=4){for(let k=0;k<3;k++)pixels.data[i+k]*=channels[k];pixels.data[i+3]*=opacity;}
        context.putImageData(pixels,0,0);
    }
    if(visible){
        const fill=document.createElement('canvas');fill.width=32;fill.height=32;const f=fill.getContext('2d');
        f.drawImage(images[state],0,0);tint(f,color,alpha);ctx.drawImage(fill,0,0);
    }
    ctx.drawImage(images.t_inactive,0,0);tint(ctx,spriteTint,spriteAlpha);return buffer;
}
