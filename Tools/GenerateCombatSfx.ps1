# Original procedural arcade sound palette. No samples from third parties.
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot '../Assets/Resources/CombatSfx'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class BulletRushSoundGenerator {
    const int Rate = 44100;
    public static void Write(string path, double seconds, double start, double end, double noise, double punch, double ring, double flutter, int kind, int seed) {
        int n = (int)(Rate*seconds); var rng = new Random(seed); var samples = new float[n];
        double phase=0, lp=0, previous=0, peak=.001;
        for(int i=0;i<n;i++) {
            double t=i/(double)Rate, u=t/seconds, f=end+(start-end)*Math.Exp(-u*4.5);
            phase += 2*Math.PI*f/Rate;
            double raw=rng.NextDouble()*2-1; lp += (raw-lp)*(.13+.25*(1-u));
            double grit=raw-lp; double env=Math.Min(1,t/.004)*Math.Pow(1-u,2.1);
            double body=Math.Sin(phase)+.23*Math.Sin(phase*2.01)+ring*Math.Sin(phase*2.73);
            double transient=Math.Exp(-t/(.012+seconds*.045));
            double texture=(kind==2 ? grit*.70+lp*.30 : lp*.8+grit*.2);
            double v=(body*(1-noise)*.57+noise*texture*(.55+transient*.65))*env;
            v+=punch*Math.Sin(2*Math.PI*(65*t+20*(1-Math.Exp(-t*30))))*Math.Exp(-t*25)*Math.Min(1,t/.002);
            if(flutter>0) v *= .72+.28*Math.Sin(t*flutter*2*Math.PI);
            if(kind==1) v*= (0.6+0.4*Math.Sin(Math.PI*u));
            if(kind==3) v += .17*Math.Sin(phase*1.5)*Math.Sin(Math.PI*u)*env;
            if(kind==4) v += .18*Math.Sin(phase*3.1)*Math.Pow(1-u,4)*(0.5+0.5*Math.Sin(t*77*2*Math.PI));
            v=Math.Tanh(v*1.8); double filtered=v-previous*.08; previous=v;
            samples[i]=(float)filtered; peak=Math.Max(peak,Math.Abs(filtered));
        }
        using(var w=new BinaryWriter(File.Create(path))) {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+n*2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);
            for(int i=0;i<n;i++)w.Write((short)(samples[i]/peak*22000*Math.Min(1,(n-i)/128.0)));
        }
    }
}
'@
# name, duration, frequency start/end, noise, low punch, harmonics, flutter, texture
$profiles = @(
    @('rifle',.105,220,85,.73,.23,.12,0,0), @('burst',.105,310,110,.67,.19,.18,32,0),
    @('gold',.13,540,180,.45,.15,.28,0,3), @('hunter',.16,200,65,.65,.31,.14,0,0),
    @('plasma',.18,610,170,.22,.10,.38,16,3), @('shotgun',.17,160,55,.83,.40,.08,0,0),
    @('heavy',.23,110,45,.76,.55,.09,0,0), @('double',.19,180,58,.79,.38,.12,24,0), @('fire',.20,220,75,.75,.24,.10,34,1),
    @('void',.22,300,49,.34,.25,.28,17,3), @('smg',.07,300,130,.63,.08,.12,0,0),
    @('assassin',.095,650,210,.38,.09,.10,0,2), @('electric',.13,750,190,.53,.06,.25,46,4),
    @('minigun',.065,150,75,.77,.12,.12,0,0), @('twin',.09,390,150,.59,.10,.18,38,0),
    @('spawn',.17,190,420,.16,.02,.10,0,1), @('explosion',.37,115,36,.76,.50,.04,0,0),
    @('slash',.23,570,145,.51,.08,.12,0,1), @('slam',.30,125,42,.58,.52,.06,0,0),
    @('frost',.32,1050,360,.31,.04,.45,12,3), @('ice_lance',.19,1400,440,.36,.04,.42,0,2), @('gravity',.34,320,42,.31,.23,.23,12,3),
    @('orbit',.13,690,360,.42,.02,.20,0,2), @('familiar',.20,480,170,.20,.05,.35,17,3),
    @('star',.38,450,43,.57,.44,.24,0,3), @('vampire',.25,230,65,.32,.22,.18,7,1),
    @('bee',.19,185,270,.23,.02,.32,53,4), @('ricochet',.13,1500,590,.22,.02,.44,0,3),
    @('heal',.28,360,760,.07,0,.23,5,3), @('shield',.27,410,880,.12,.04,.42,10,3),
    @('hit',.075,185,78,.67,.04,.06,0,0), @('shatter',.15,420,130,.72,.06,.12,24,2),
    @('thunder',.24,920,135,.62,.13,.27,39,4), @('flame_ring',.31,290,75,.68,.15,.15,23,1),
    @('pop',.13,320,75,.31,.20,.08,0,0), @('fall',.16,145,49,.47,.22,.04,0,0),
    @('impact_plasma',.09,770,240,.24,.03,.36,13,3), @('impact_fire',.085,300,90,.78,.04,.07,17,1),
    @('impact_void',.10,260,65,.31,.11,.23,11,3), @('impact_electric',.08,1030,295,.66,.01,.17,54,4)
)
$seed = 184
foreach ($p in $profiles) {
    $name = $p[0]
    $assetPath = Join-Path $destination "$name.wav"
    [BulletRushSoundGenerator]::Write($assetPath,$p[1],$p[2],$p[3],$p[4],$p[5],$p[6],$p[7],$p[8],(++$seed))
    if (!(Test-Path "$assetPath.meta")) {
        $template = Get-Content (Join-Path $PSScriptRoot '../Assets/Audio/SFX/enemy_spawn.wav.meta') -Raw
        $template = $template -replace '(?m)^guid: [0-9a-f]+', ('guid: '+[guid]::NewGuid().ToString('N'))
        $template = $template -replace 'normalize: 1', 'normalize: 0'
        [IO.File]::WriteAllText("$assetPath.meta",$template)
    }
}
if (!(Test-Path "$destination.meta")) {
    $template = Get-Content (Join-Path $PSScriptRoot '../Assets/Resources/CharacterAbilities.meta') -Raw
    $template = $template -replace '(?m)^guid: [0-9a-f]+', ('guid: '+[guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText("$destination.meta",$template)
}
