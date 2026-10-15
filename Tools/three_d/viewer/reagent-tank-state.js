export function tankComposition(model, {visible=false,color='#FFFFFF',alpha=1,spriteTint='#FFFFFF',spriteAlpha=1}={}) {
    if (!model?.reagentTankAppearance || !/^#[0-9a-f]{6}$/i.test(color) || !/^#[0-9a-f]{6}$/i.test(spriteTint) ||
        !Number.isFinite(alpha) || alpha<0 || alpha>1 || spriteAlpha!==1)
        throw new Error('Unsupported tank appearance.');
    const rgb = value => [1,3,5].map(i=>parseInt(value.slice(i,i+2),16)/255);
    const fill=rgb(color), overall=[...rgb(spriteTint),spriteAlpha], a=visible?alpha:0;
    const vessel=[...fill.map(c=>1-a+a*c),1], labels=new Set(model.reagentTankAppearance.vesselParts);
    return model.parts.map(part=>{
        const base=[...rgb(part.color||'#FFFFFF'),part.color?.length===9?parseInt(part.color.slice(7,9),16)/255:1];
        const tint=labels.has(part.label)?vessel:[1,1,1,1];
        return {...part,color:'#'+base.map((c,i)=>Math.round(c*tint[i]*overall[i]*255).toString(16).padStart(2,'0')).join('').toUpperCase()};
    });
}
