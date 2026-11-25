# Original, offline layered game SFX. Existing weapon and spawn WAVs are untouched.
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot '../Assets/Resources/CombatSfx'
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class LayeredArcadeSfx {
    const int Rate=44100;
    const double Tau=2*Math.PI;
    static double E(double t,double decay) { return Math.Exp(-Math.Max(0,t)/decay); }
    static double S(double f,double t) { return Math.Sin(Tau*f*t); }
    static double Chirp(double t,double f,double end,double decay) { return Math.Sin(Tau*(end*t+(f-end)*decay*(1-Math.Exp(-t/decay)))); }
    public static void Write(string path,int kind,double duration,double pitch,double weight,int seed) {
        int n=(int)(Rate*duration); var rng=new Random(seed); var dry=new double[n]; var output=new double[n];
        double sub=0,low=0,mid=0,prev=0,peak=0;
        for(int i=0;i<n;i++) {
            double t=i/(double)Rate,u=t/duration,noise=rng.NextDouble()*2-1;
            sub+=(noise-sub)*.012; low+=(noise-low)*.055; mid+=(noise-mid)*.22;
            double high=noise-mid, band=mid-low, bass=low-sub, v=0;
            switch(kind) {
                case 0: // layered arcade detonation: snap, pressure, rolling low tail and scattered debris
                    v=.28*high*E(t,.008)+.60*band*E(t,.035)+weight*.65*Chirp(t,105*pitch,39*pitch,.043)*E(t,.105)
                       +1.25*bass*E(t,.16)+.95*sub*E(t,.23)+.20*Chirp(t,220*pitch,83*pitch,.036)*E(t,.046);
                    v+=.22*band*(.6+.4*S(17,t))*E(t,.19)+.12*high*E(Math.Abs(t-.08),.019);
                    break;
                case 1: // metallic air cut and ringing edge
                    v=.92*band*Math.Sin(Math.PI*Math.Min(1,t/.15))*E(t,.105)+.35*high*E(Math.Abs(t-.065),.018)
                      +.035*S(870*pitch,t)*E(t,.023)+.23*Chirp(t,480*pitch,125*pitch,.045)*E(t,.05);
                    break;
                case 2: // ice/glass, separate crack and irregular resonators
                    v=.68*high*E(t,.016)+.36*band*E(t,.08)+.07*S(1430*pitch,t)*E(t,.045)+.035*S(2090*pitch,t)*E(t,.054)
                      +.05*S(2710*pitch,t)*E(t,.035)+.23*high*E(Math.Abs(t-.055),.012)+.16*Chirp(t,380*pitch,160*pitch,.04)*E(t,.085);
                    break;
                case 3: // burning air / expanding crackle
                    v=.6*band*(.65+.35*S(31,t))*E(t,.16)+.38*bass*E(t,.12)+.24*high*(rng.NextDouble()>.90?1:.18)*E(t,.20)
                      +.20*Chirp(t,190*pitch,68*pitch,.06)*E(t,.11);
                    break;
                case 4: // gravity, shadow: rising suction then a deep answering pulse
                    v=.35*Chirp(t,70*pitch,300*pitch,.08)*Math.Min(1,t/.065)*E(t,.17)+.28*Chirp(t,270*pitch,47*pitch,.065)*E(t,.20)
                      +.17*S(93*pitch,t)*S(6,t)*E(t,.18)+.4*bass*E(t,.17)+.15*band*E(t,.10);
                    break;
                case 5: // electric arcs: alternating bright bursts and lower discharge
                    v=.43*high*(.45+.55*Math.Abs(S(49*pitch,t)))*E(t,.085)+.28*Chirp(t,1350*pitch,235*pitch,.035)*E(t,.105)
                      +.20*S(540*pitch,t)*Math.Abs(S(37,t))*E(t,.11)+.24*Chirp(t,140*pitch,54*pitch,.045)*E(t,.09);
                    break;
                case 6: // musical shield/healing, layered ascending chime
                    v=.36*Chirp(t,290*pitch,660*pitch,.06)*E(t,.16)+.24*S(660*pitch,t)*E(t,.19)+.17*S(990*pitch,t)*E(t,.15)
                      +.15*S(1320*pitch,t)*E(t,.085)+.08*band*E(t,.04);
                    break;
                case 7: // unmistakable player damage: tight low thud and descending warning tone
                    v=.56*Chirp(t,155,64,.031)*E(t,.075)+.4*band*E(t,.025)+.16*high*E(t,.008)
                      +.24*Chirp(t,440,185,.065)*E(t,.105)+.17*S(124,t)*E(t,.12);
                    break;
                case 8: // insect-like fantasy swarm with distinct modulated harmonics
                    v=.26*S(190*pitch,t+.0007*S(32,t))*(.65+.35*S(47,t))*E(t,.12)+.14*S(390*pitch,t)*E(t,.09)
                      +.33*band*Math.Abs(S(52,t))*E(t,.10)+.10*high*E(t,.04);
                    break;
                case 10: // soft UI press, short rounded tactile click
                    v=.22*band*E(t,.013)+.42*Chirp(t,460*pitch,230*pitch,.013)*E(t,.023)+.06*high*E(t,.006);
                    break;
                case 11: // gentle navigation / selection
                    v=.31*Chirp(t,520*pitch,740*pitch,.024)*E(t,.045)+.12*S(880*pitch,t)*E(t,.025)+.13*band*E(t,.012);
                    break;
                case 12: // confirmation, two delicate tones
                    v=.33*S(440*pitch,t)*E(t,.05)+.20*S(660*pitch,t)*E(t,.065);
                    if(t>.038) v+=.22*S(880*pitch,t-.038)*E(t-.038,.045)*Math.Min(1,(t-.038)/.006);
                    break;
                case 13: // back / close
                    v=.38*Chirp(t,640*pitch,260*pitch,.023)*E(t,.047)+.12*band*E(t,.015);
                    break;
                case 14: // cinematic low pressure pulse, with a soft air tail
                    v=.57*Chirp(t,118,39,.062)*E(t,.19)+.28*bass*E(t,.18)+.22*band*Math.Min(1,t/.045)*E(t,.15)+.15*Chirp(t,240,71,.08)*E(t,.14);
                    break;
                default: // compact ricochet / shatter, no prolonged metallic ring
                    v=.62*high*E(t,.008)+.36*band*E(t,.035)+.095*S(1100*pitch,t)*E(t,.020)+.045*S(1750*pitch,t)*E(t,.022)
                      +.24*Chirp(t,290*pitch,80*pitch,.035)*E(t,.05);
                    break;
            }
            double edge=Math.Min(1,t/.0018)*Math.Min(1,(1-u)*12);
            dry[i]=Math.Tanh(v*1.65)*edge;
        }
        // Short baked early reflections add depth without Unity reverb filters or extra voices.
        int a=(int)(Rate*.019),b=(int)(Rate*.043),c=(int)(Rate*.071),d=(int)(Rate*.109);
        for(int i=0;i<n;i++) {
            double reflection=kind>=10?0:kind==1||kind==2||kind==9?.35:1;
            double tail=reflection*((i>=a?.14*dry[i-a]:0)+(i>=b?.10*dry[i-b]:0)+(i>=c?.065*dry[i-c]:0)+(i>=d?.027*dry[i-d]:0));
            double fade=Math.Min(1,(n-i)/(Rate*.035));
            double v=(dry[i]+tail)*fade; output[i]=v-prev*.045; prev=v;peak=Math.Max(peak,Math.Abs(output[i]));
        }
        using(var w=new BinaryWriter(File.Create(path))) {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);
            for(int i=0;i<n;i++)w.Write((short)(output[i]/Math.Max(.001,peak)*25500));
        }
    }
}
'@
# name, layer design, duration, pitch, weight
$profiles = @(
    @('blast_round',0,.46,1.23,.69), @('blast_bomb',0,.72,.92,1.05), @('blast_boss',0,.87,.75,1.18),
    @('blast_hazard',0,.62,1.02,.92), @('blast_crate',0,.36,1.55,.48), @('blast_meteor',0,.66,1.04,1.08),
    @('slash_depth',1,.29,1.1,1), @('slam_depth',0,.48,.83,.93), @('frost_depth',2,.43,.86,1),
    @('ice_lance_depth',2,.25,1.38,1), @('ice_break',2,.22,.72,1), @('gravity_depth',4,.61,.83,1),
    @('void_burst',4,.42,.62,1), @('orbit_depth',1,.19,1.55,1), @('familiar_depth',4,.34,1.45,1),
    @('lightning_storm_depth',5,.42,.85,1), @('chain_discharge',5,.23,1.27,1), @('fire_ring_depth',3,.48,.83,1),
    @('fire_ignition',3,.18,1.42,1), @('fire_rupture',3,.31,.72,1), @('vampire_depth',4,.39,1.02,1),
    @('swarm_depth',8,.32,1.0,1), @('ricochet_volley_depth',9,.27,.83,1), @('heal_depth',6,.43,1.16,1),
    @('shield_depth',6,.37,.72,1), @('player_hurt_depth',7,.24,1,1), @('shatter_depth',9,.24,.70,1),
    @('ricochet_depth',9,.17,1.44,1), @('pop_depth',0,.19,1.8,.40), @('fall_depth',0,.23,.95,.34),
    @('ui_press',10,.085,1,1), @('ui_select',11,.11,1,1), @('ui_confirm',12,.16,1,1), @('ui_back',13,.105,1,1),
    @('cinematic_hit',14,.52,1,1)
)
$seed=519
foreach($p in $profiles) {
    $assetPath=Join-Path $destination ($p[0]+'.wav')
    [LayeredArcadeSfx]::Write($assetPath,$p[1],$p[2],$p[3],$p[4],(++$seed))
    if (!(Test-Path "$assetPath.meta")) {
        $template=Get-Content (Join-Path $PSScriptRoot '../Assets/Audio/SFX/enemy_spawn.wav.meta') -Raw
        $template=$template -replace '(?m)^guid: [0-9a-f]+',('guid: '+[guid]::NewGuid().ToString('N'))
        $template=$template -replace 'normalize: 1','normalize: 0'
        [IO.File]::WriteAllText("$assetPath.meta",$template)
    }
}
