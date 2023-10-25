shader_type canvas_item;

const int samples = 35,
          LOD = 2,         // gaussian done on MIPmap at scale LOD
          sLOD = 1 << LOD; // tile size = 2^LOD
const float sigma=float(samples)*.25;
uniform int strength: hint_range(1, 512) = 2;
uniform sampler2D SCREEN2D:hint_screen_texture,filter_linear_mipmap;
float gaussian(vec2 i) {
    return exp( -.5* dot(i/=sigma,i) ) / ( 6.28 * sigma*sigma );
}
vec4 blur_size(sampler2D tex,vec2 fragCoord, vec2 pixelSize) {
    
    vec4 color = vec4(0.,0.,0.,0.);
	int s=samples/sLOD;
	for (int i=0; i<s*s;i++){
		vec2 d=vec2(float(i%s),float(i/s))*float(sLOD)-float(samples)/2.;
		color+=gaussian(d)*(textureLod(tex,fragCoord+pixelSize*d,float(LOD)));
		
	}
	return color/color.a;
}


void fragment() {
	vec2 res=1./SCREEN_PIXEL_SIZE;	
    COLOR = blur_size(SCREEN2D,SCREEN_UV/res.xy,SCREEN_PIXEL_SIZE);
}