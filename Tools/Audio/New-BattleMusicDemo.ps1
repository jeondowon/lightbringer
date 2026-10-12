param(
    [string]$OutputPath,
    [ValidateSet('FirstLight', 'WarMarch')]
    [string]$Variant = 'FirstLight'
)

# Original procedural composition. No samples, network requests, or dependencies.
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) {
    $OutputPath = Join-Path $PSScriptRoot "../../Assets/Audio/Music/Lightbringer_${Variant}_Demo.wav"
}
Add-Type -TypeDefinition @'
using System;
using System.IO;

public static class FirstLightMusic
{
    const int Rate = 44100;
    static double Beat = 60.0 / 112.0;
    static bool warMarch;
    static float[] left, right;
    static readonly Random noise = new Random(4812);
    static double Freq(int note) { return 440 * Math.Pow(2, (note - 69) / 12.0); }
    static double Sin(double phase) { return Math.Sin(2 * Math.PI * phase); }
    static double Attack(double t, double seconds) { return Math.Min(1, t / seconds); }

    static void Note(double startBeat, double beats, int midi, double level, double pan, int voice)
    {
        double duration = beats * Beat;
        double tail = voice == 0 ? 0.7 : voice == 1 ? 1.1 : voice == 4 ? 0.035 : 0.12;
        int start = (int)Math.Round(startBeat * Beat * Rate);
        int count = (int)((duration + tail) * Rate);
        double freq = Freq(midi);
        double lg = Math.Sqrt((1 - pan) * 0.5), rg = Math.Sqrt((1 + pan) * 0.5);
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / Rate;
            double release = t <= duration ? 1 : Math.Pow(Math.Max(0, 1 - (t-duration)/tail), 2);
            double value;
            if (voice == 0) // Warm string-like pad with a slow attack and detuning.
            {
                double p = freq * t;
                value = (Sin(p) + 0.38*Sin(p*1.003) + 0.23*Sin(p*0.997)
                    + 0.18*Sin(p*2) + 0.07*Sin(p*3)) / 1.86;
                value *= Attack(t, 0.25) * release * (0.9 + 0.1*Sin(t*0.7));
            }
            else if (voice == 1) // Bell/harp hybrid, with a gentle transient.
            {
                value = Sin(freq*t)*Math.Exp(-t*2.0)
                    + 0.28*Sin(freq*2*t)*Math.Exp(-t*4.0)
                    + 0.09*Sin(freq*3.002*t)*Math.Exp(-t*7);
                value *= Attack(t, 0.009) * release / 1.37;
            }
            else if (voice == 2) // Rounded bass.
            {
                value = (Sin(freq*t) + 0.25*Sin(freq*2*t) + 0.08*Sin(freq*3*t))/1.33;
                value *= Attack(t, 0.016)*Math.Exp(-t*0.8)*release;
            }
            else if (voice == 3) // Airy lead.
            {
                double p = freq*t + 0.0025*Sin(4.8*t);
                value = (Sin(p) + 0.2*Sin(2*p) + 0.07*Sin(3*p))/1.27;
                value *= Attack(t, 0.045)*release*(0.85+0.15*Math.Exp(-t*3));
            }
            else if (voice == 4) // Bowed-string ostinato: sharp, short, harmonically rich.
            {
                double p = freq*t;
                value = (Sin(p)+0.48*Sin(2*p)+0.32*Sin(3*p)+0.22*Sin(4*p)
                    +0.13*Sin(5*p)+0.08*Sin(6*p)+0.22*Sin(p*1.004))/2.45;
                value *= Attack(t,0.007)*Math.Exp(-t*5.0)*release;
            }
            else // Brass-like theme with a bright attack and a darker sustained body.
            {
                double p = freq*t + 0.003*Sin(5*t)*Attack(t,0.3);
                double brightness = 0.5+0.5*Math.Exp(-t*5);
                value = (Sin(p)+0.5*Sin(p*1.002)
                    +brightness*(0.6*Sin(2*p)+0.35*Sin(3*p)+0.18*Sin(4*p)+0.09*Sin(5*p)))/2.82;
                value *= Attack(t,0.015)*release*(0.82+0.18*Math.Exp(-t*7));
            }
            int index = (start+i)%left.Length;
            left[index] += (float)(value*level*lg);
            right[index] += (float)(value*level*rg);
        }
    }

    static void Drum(double beat, double level, int kind, double pan)
    {
        int start = (int)Math.Round(beat*Beat*Rate);
        double duration = kind == 0 ? 0.65 : kind == 1 ? (warMarch ? 0.25 : 0.22) : kind == 3 ? 0.5 : kind == 4 ? 1.6 : 0.075;
        double previous = 0;
        for (int i=0; i<(int)(duration*Rate); i++)
        {
            double t = (double)i/Rate;
            double n = noise.NextDouble()*2-1;
            double value;
            if (kind == 0)
            {
                double phase = 49*t + 2.2*(1-Math.Exp(-t*26));
                value = Sin(phase)*Math.Exp(-t*8) + 0.06*n*Math.Exp(-t*75);
            }
            else if (kind == 1)
                value = warMarch
                    ? (0.48*n + 0.17*(n-previous) + 0.35*Sin(165*t))*Math.Exp(-t*18)
                    : (0.55*n + 0.45*Sin(165*t))*Math.Exp(-t*24);
            else if (kind == 3)
                value = (Sin(87*t+1.4*(1-Math.Exp(-t*18)))
                    +0.32*Sin(139*t))*Math.Exp(-t*9)+0.08*n*Math.Exp(-t*45);
            else if (kind == 4)
                value = ((n-previous)*0.45 + 0.12*Sin(3911*t)*Sin(5287*t))*Math.Exp(-t*2.3);
            else
                value = (n-previous)*0.45*Math.Exp(-t*70);
            previous = n;
            value *= Attack(t, 0.003)*Math.Pow(1-t/duration, 2)*level;
            int index = (start+i)%left.Length;
            left[index] += (float)(value*Math.Sqrt((1-pan)*0.5));
            right[index] += (float)(value*Math.Sqrt((1+pan)*0.5));
        }
    }

    static void ArrangeWarMarch()
    {
        // Original D-minor march, two different eight-bar phrases.
        // 0-8: theme A; 8-16: full theme B; 16-24: low-register response;
        // 24-32: theme B returns in octaves. Percussion continues throughout.
        int[][] chords = {
            new[]{50,57,62,65}, new[]{50,57,62,65},
            new[]{46,53,58,62}, new[]{48,55,60,64},
            new[]{43,50,58,62}, new[]{46,53,58,62},
            new[]{45,52,57,61}, new[]{45,52,57,61}
        };
        int[] roots = {38,38,34,36,31,34,33,33};
        int[][] themeA = {
            new[]{74,74,77,76,74,69,72}, new[]{74,77,81,79,77,76,74},
            new[]{70,74,77,74,70,72,74}, new[]{76,79,76,72,71,72,76},
            new[]{74,79,77,74,70,69,67}, new[]{70,74,77,81,77,74,70},
            new[]{73,76,81,79,76,73,71}, new[]{73,76,73,69,73,76,81}
        };
        int[][] themeB = {
            new[]{81,81,84,81,77,79,81}, new[]{86,84,81,79,77,76,74},
            new[]{82,81,77,74,77,79,82}, new[]{79,76,72,76,79,81,79},
            new[]{79,82,81,79,77,74,70}, new[]{77,81,82,81,77,74,77},
            new[]{81,80,81,76,73,76,79}, new[]{81,79,76,73,69,73,76}
        };
        double[] rhythm = {0.75,0.25,0.5,0.5,1,0.5,0.5};
        int[] ostinato = {0,1,2,1,0,1,3,2,0,1,2,3,2,1,3,1};
        for(int bar=0;bar<32;bar++)
        {
            int c=bar%8;
            double at=bar*4;
            bool response=bar>=16 && bar<24;
            bool full=bar>=8 && !response;
            double energy=response?0.82:full?1.0:0.91;
            for(int j=0;j<16;j++)
            {
                int pitch=chords[c][ostinato[j]];
                double accent=j%4==0?1.22:j%2==0?1.0:0.76;
                Note(at+j*0.25,0.18,pitch,0.14*accent*energy,-0.5,4);
                Note(at+j*0.25+0.014,0.16,pitch+12,0.057*accent*energy,0.53,4);
            }
            for(int j=0;j<8;j++)
                Note(at+j*0.5,0.37,roots[c],(j%2==0?0.24:0.17)*energy,0,2);
            for(int j=0;j<4;j++)
                Note(at,3.7,chords[c][j],0.035,-0.65+j*0.43,0);
            double position=at;
            int[] phrase=(full?themeB:themeA)[c];
            for(int j=0;j<phrase.Length;j++)
            {
                int pitch=phrase[j]-(response?12:0);
                Note(position,rhythm[j]*0.83,pitch,0.22*energy,0.03,5);
                if(full)
                    Note(position+0.008,rhythm[j]*0.8,pitch-12,0.085,-0.18,5);
                position+=rhythm[j];
            }
            if(full)
            {
                foreach(double hit in new[]{0.0,1.5,3.0})
                    for(int j=1;j<4;j++)
                        Note(at+hit,0.32,chords[c][j],0.09,-0.32+j*0.2,5);
            }
            Drum(at,0.56*energy,0,0);
            Drum(at+1.5,0.31*energy,0,0);
            Drum(at+2,0.46*energy,0,0);
            Drum(at+3.5,0.33*energy,0,0);
            Drum(at+1,0.27*energy,1,-0.12);
            Drum(at+3,0.30*energy,1,0.12);
            Drum(at+0.75,0.19*energy,3,-0.4);
            Drum(at+2.75,0.23*energy,3,0.4);
            for(int j=0;j<8;j++)
                Drum(at+j*0.5,(j%2==0?0.054:0.038)*energy,2,0.33);
            if(bar%4==0) Drum(at,0.19,4,-0.25);
            if(bar%4==3)
            {
                for(int j=0;j<4;j++)
                    Drum(at+3+j*0.25,0.13+j*0.025,j%2==0?1:3,-0.4+j*0.25);
            }
        }
    }

    public static void Render(string path, string variant)
    {
        bool march = variant == "WarMarch";
        warMarch = march;
        Beat = 60.0 / (march ? 140 : 112);
        int length = (int)Math.Round(32*4*Beat*Rate);
        left = new float[length]; right = new float[length];
        if(march) ArrangeWarMarch();
        else {
        // D minor, Bb major, F major, C major; second phrase resolves through A.
        int[][] chords = {
            new[]{50,57,62,65}, new[]{46,53,58,62},
            new[]{48,57,60,65}, new[]{48,55,60,64},
            new[]{50,57,62,65}, new[]{46,53,58,62},
            new[]{43,55,58,62}, new[]{45,57,61,64}
        };
        int[] roots = {38,34,41,36,38,34,31,33};
        int[][] melodies = {
            new[]{74,77,76,74}, new[]{74,77,81,77},
            new[]{72,77,79,81}, new[]{79,76,74,72},
            new[]{74,77,81,84}, new[]{82,81,77,74},
            new[]{79,77,74,70}, new[]{73,76,81,73}
        };
        int[] arp = {1,2,3,2,1,3,2,3};
        for (int bar=0; bar<32; bar++)
        {
            int c = bar%8;
            double at = bar*4;
            bool quiet = bar>=16 && bar<24;
            double strength = quiet ? 0.68 : bar>=24 ? 1.05 : 0.9;
            for (int j=0; j<4; j++)
                Note(at,3.8,chords[c][j],0.082*strength,-0.7+j*0.46,0);
            for (int j=0; j<8; j++)
                Note(at+j*0.5,0.3,chords[c][arp[j]]+12,0.069*strength,
                    j%2==0 ? -0.38 : 0.38,1);
            Note(at,1.6,roots[c],0.20*strength,0,2);
            Note(at+2,1.55,roots[c],0.17*strength,0,2);
            if (bar>=8 && !quiet)
            {
                for (int j=0; j<4; j++)
                    Note(at+j,0.76,melodies[c][j],0.115,j%2==0 ? -0.08 : 0.08,3);
            }
            else if (quiet)
            {
                Note(at,1.5,melodies[c][0],0.10,-0.16,1);
                Note(at+2,1.3,melodies[c][2],0.075,0.20,1);
            }
            Drum(at,0.37*strength,0,-0.08);
            Drum(at+2,0.30*strength,0,0.08);
            if (!quiet)
            {
                Drum(at+1,0.105,1,-0.22);
                Drum(at+3,0.12,1,0.22);
                for (int j=0; j<8; j++)
                    Drum(at+j*0.5,j%2==0?0.041:0.027,2,0.40);
                if (bar%8==7) Drum(at+3.5,0.17,0,-0.25);
            }
        }
        }

        // Circular delay/reverb carries tails across the loop boundary.
        float[] dryL = (float[])left.Clone(), dryR = (float[])right.Clone();
        double[] delays = {0.137,0.223,Beat*0.75,Beat*1.5,0.719,1.071};
        double[] gains = march ? new[]{0.05,0.04,0.06,0.03,0.025,0.018}
            : new[]{0.065,0.055,0.13,0.065,0.045,0.028};
        for (int tap=0; tap<delays.Length; tap++)
        {
            int shift=(int)(delays[tap]*Rate);
            for(int i=0;i<length;i++)
            {
                int source=(i-shift+length)%length;
                left[i] += (float)(dryR[source]*gains[tap]);
                right[i] += (float)(dryL[source]*gains[tap]);
            }
        }
        double meanL=0,meanR=0;
        for(int i=0;i<length;i++){meanL+=left[i];meanR+=right[i];}
        meanL/=length;meanR/=length;
        double peak=0;
        for(int i=0;i<length;i++)
        {
            left[i]-=(float)meanL; right[i]-=(float)meanR;
            peak=Math.Max(peak,Math.Max(Math.Abs(left[i]),Math.Abs(right[i])));
        }
        if(march)
        {
            // Gentle bus saturation restrains drum transients and brings the ensemble forward.
            double drive=2.0/peak;
            peak=0;
            for(int i=0;i<length;i++)
            {
                left[i]=(float)Math.Tanh(left[i]*drive);
                right[i]=(float)Math.Tanh(right[i]*drive);
                peak=Math.Max(peak,Math.Max(Math.Abs(left[i]),Math.Abs(right[i])));
            }
        }
        double peakDb=march?-2.0:-3.0;
        double gain=Math.Pow(10,peakDb/20)/peak;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        double squares=0;
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36+length*4);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)2);
            writer.Write(Rate);writer.Write(Rate*4);writer.Write((short)4);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(length*4);
            for(int i=0;i<length;i++)
            {
                double l=left[i]*gain,r=right[i]*gain;
                squares+=l*l+r*r;
                writer.Write((short)Math.Round(l*32767));writer.Write((short)Math.Round(r*32767));
            }
        }
        Console.WriteLine("Created: "+Path.GetFullPath(path));
        Console.WriteLine("Duration: "+((double)length/Rate).ToString("F3")+" s | "+(march?140:112)+" BPM | 32 bars | 44.1 kHz stereo PCM16");
        Console.WriteLine("Peak: "+peakDb.ToString("F2")+" dBFS | RMS: "+(20*Math.Log10(Math.Sqrt(squares/(2*length)))).ToString("F2")+" dBFS");
        Console.WriteLine("Loop boundary delta L/R: "+(Math.Abs(left[0]-left[length-1])*gain).ToString("F6")+" / "+(Math.Abs(right[0]-right[length-1])*gain).ToString("F6"));
    }
}
'@
[FirstLightMusic]::Render($OutputPath, $Variant)
