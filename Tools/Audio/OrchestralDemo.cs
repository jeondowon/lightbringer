// Offline sample-based rendering. All audible instruments are VSCO 2 CE recordings.
using System;
using System.IO;
using System.Collections.Generic;

public sealed class LightbringerOrchestralDemo
{
    const int Rate = 44100;
    const double Beat = 60.0 / 128;
    const int Bars = 32;
    sealed class Sample
    {
        public float[] Data;
        public int Root, Rate;
    }
    readonly Dictionary<string,List<Sample>> banks = new Dictionary<string,List<Sample>>();
    readonly Random random = new Random(861);
    float[] left = new float[(int)Math.Round(Bars*4*Beat*Rate)];
    float[] right = new float[(int)Math.Round(Bars*4*Beat*Rate)];
    int voices;

    public void Load(string bank, string path, int root)
    {
        using(var reader = new BinaryReader(File.OpenRead(path)))
        {
            if(new string(reader.ReadChars(4))!="RIFF") throw new Exception("Not RIFF: "+path);
            reader.ReadInt32();
            if(new string(reader.ReadChars(4))!="WAVE") throw new Exception("Not WAVE: "+path);
            int format=0,channels=0,rate=0,bits=0;
            byte[] data=null;
            while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
            {
                string chunk=new string(reader.ReadChars(4));
                int size=reader.ReadInt32();
                long next=reader.BaseStream.Position+size+(size%2);
                if(size<0 || next>reader.BaseStream.Length) throw new Exception("Invalid chunk: "+path);
                if(chunk=="fmt ")
                {
                    format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();
                    reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();
                    if(format==65534 && size>=40)
                    {
                        reader.ReadInt16();reader.ReadInt16();reader.ReadInt32();format=reader.ReadInt16();
                    }
                }
                else if(chunk=="data") data=reader.ReadBytes(size);
                reader.BaseStream.Position=next;
            }
            if(data==null || channels<1 || (format!=1 && format!=3) || (bits!=16 && bits!=24 && bits!=32))
                throw new Exception("Unsupported WAV: "+path);
            int bytes=bits/8,frames=data.Length/(channels*bytes);
            float[] mono=new float[frames];
            double peak=0,mean=0;
            for(int i=0;i<frames;i++)
            {
                double value=0;
                for(int c=0;c<channels;c++)
                {
                    int at=(i*channels+c)*bytes;
                    if(format==3 && bits==32) value+=BitConverter.ToSingle(data,at);
                    else if(bits==16) value+=BitConverter.ToInt16(data,at)/32768.0;
                    else if(bits==24)
                    {
                        int v=data[at] | data[at+1]<<8 | data[at+2]<<16;
                        if((v & 0x800000)!=0) v|=unchecked((int)0xff000000);
                        value+=v/8388608.0;
                    }
                    else value+=BitConverter.ToInt32(data,at)/2147483648.0;
                }
                mono[i]=(float)(value/channels);mean+=mono[i];
            }
            mean/=frames;
            for(int i=0;i<frames;i++){mono[i]-=(float)mean;peak=Math.Max(peak,Math.Abs(mono[i]));}
            if(peak<0.00001) throw new Exception("Silent sample: "+path);
            int onset=0;
            while(onset<Math.Min(frames-1,rate/3) && Math.Abs(mono[onset])<peak*0.012) onset++;
            onset=Math.Max(0,onset-rate/250);
            float[] trimmed=new float[frames-onset];
            for(int i=0;i<trimmed.Length;i++) trimmed[i]=(float)(mono[i+onset]/peak*0.8);
            if(!banks.ContainsKey(bank)) banks.Add(bank,new List<Sample>());
            banks[bank].Add(new Sample{Data=trimmed,Root=root,Rate=rate});
        }
    }

    // Four-point interpolation keeps transposed recordings smooth.
    static double Read(float[] data,double pos)
    {
        int i=(int)pos; double x=pos-i;
        double a=data[Math.Max(0,i-1)],b=data[i],c=data[Math.Min(data.Length-1,i+1)],d=data[Math.Min(data.Length-1,i+2)];
        return b+0.5*x*(c-a+x*(2*a-5*b+4*c-d+x*(3*(b-c)+d-a)));
    }

    void Play(string bank,double beat,double duration,int pitch,double volume,double pan,bool oneShot=false)
    {
        int distance=999;
        List<Sample> options=new List<Sample>();
        foreach(Sample candidate in banks[bank])
        {
            int delta=Math.Abs(candidate.Root-pitch);
            if(delta<distance){distance=delta;options.Clear();}
            if(delta==distance) options.Add(candidate);
        }
        Sample sample=options[voices++%options.Count];
        double speed=Math.Pow(2,(pitch-sample.Root)/12.0)*sample.Rate/Rate;
        double release=oneShot?0.02:bank=="strings"||bank=="cello"?0.085:0.14;
        double seconds=oneShot?(sample.Data.Length-2)/speed/Rate:duration*Beat+release;
        int count=Math.Min((int)(seconds*Rate),(int)((sample.Data.Length-2)/speed));
        int start=(int)Math.Round((beat*Beat+(random.NextDouble()-0.5)*0.009)*Rate);
        double gain=volume*(0.96+random.NextDouble()*0.08);
        double l=Math.Sqrt((1-pan)*0.5)*gain,r=Math.Sqrt((1+pan)*0.5)*gain;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/Rate;
            double envelope=Math.Min(1,t/0.004)*Math.Min(1,(count-i)/(Rate*0.015));
            if(!oneShot && t>duration*Beat) envelope*=Math.Pow(Math.Max(0,1-(t-duration*Beat)/release),1.5);
            double value=Read(sample.Data,i*speed)*envelope;
            int at=(start+i+left.Length)%left.Length;
            left[at]+=(float)(value*l);right[at]+=(float)(value*r);
        }
    }

    public void Render(string path)
    {
        int[][] chords={new[]{62,65,69},new[]{58,62,65},new[]{55,58,62},new[]{57,61,64},
            new[]{62,65,69},new[]{60,65,69},new[]{55,58,62},new[]{57,61,64}};
        int[] roots={38,34,43,45,38,41,43,45};
        int[][] theme={
            new[]{74,69,74,77,76,74},new[]{77,74,70,74,77,79},
            new[]{79,74,70,74,77,74},new[]{73,76,81,79,76,73},
            new[]{74,77,81,77,76,74},new[]{77,72,77,81,79,77},
            new[]{79,77,74,70,74,77},new[]{76,73,69,73,76,73}};
        double[] rhythm={0.75,0.25,0.5,0.5,1,1};
        int[] pattern={0,2,1,2,0,1,2,1};
        for(int bar=0;bar<Bars;bar++)
        {
            int c=bar%8,section=bar/8;
            double at=bar*4;
            // Bowed eighth notes and acoustic cello keep the pulse underneath every section.
            for(int j=0;j<8;j++)
            {
                int pitch=chords[c][pattern[j]]+12;
                Play("strings",at+j*0.5,0.32,pitch,j%2==0?0.18:0.125,-0.42);
                if(j%2==0 || section==3)
                    Play("cello",at+j*0.5,0.35,roots[c]+(j%2==0?0:7),0.20,0.24);
            }
            string lead=section==1?"violin":section==2?"flute":"trumpet";
            double leadGain=lead=="trumpet"?0.36:lead=="flute"?0.43:0.32;
            double position=at;
            for(int j=0;j<6;j++)
            {
                int pitch=theme[c][j];
                Play(lead,position,rhythm[j]*0.92,pitch,leadGain,lead=="violin"?-0.24:lead=="flute"?0.2:-0.08);
                if(section==3) Play("violin",position+0.015,rhythm[j]*0.90,pitch,0.14,-0.36);
                position+=rhythm[j];
            }
            if(section!=2)
            {
                // Flute responds above the brass/violin phrase instead of a synthetic pad.
                Play("flute",at+2.5,0.42,theme[c][4]+(theme[c][4]<76?12:0),0.15,0.30);
                Play("flute",at+3,0.85,theme[c][5]+(theme[c][5]<76?12:0),0.17,0.30);
            }
            if(section==1 || section==3)
            {
                Play("trumpet",at,1.3,chords[c][0]+12,0.12,0.1);
                Play("trumpet",at+2,1.3,chords[c][2],0.11,0.12);
            }
            if(section==2)
            {
                Play("violin",at,1.7,chords[c][1]+12,0.12,-0.3);
                Play("violin",at+2,1.7,chords[c][2]+12,0.13,-0.3);
            }
            Play("bassdrum",at,0,60,0.32,0,true);
            Play("bassdrum",at+2,0,60,0.24,0,true);
            Play("snare",at+1,0,60,section==2?0.085:0.13,0.12,true);
            Play("snare",at+3,0,60,section==2?0.10:0.15,0.12,true);
            if(bar%4==3)
            {
                Play("snare",at+3.5,0,60,0.075,0.12,true);
                Play("snare",at+3.75,0,60,0.095,0.12,true);
            }
            if(bar%8==0) Play("cymbal",at,0,60,0.12,-0.15,true);
        }
        // Short, diffuse hall reflections. No synthetic instruments or electronic drums.
        float[] dryL=(float[])left.Clone(),dryR=(float[])right.Clone();
        double[] delays={0.047,0.071,0.113,0.173,0.241,0.337,0.463,0.619,0.811};
        for(int tap=0;tap<delays.Length;tap++)
        {
            int offset=(int)(delays[tap]*Rate);
            double gain=0.095*Math.Exp(-delays[tap]*2.5),smoothL=0,smoothR=0;
            for(int i=0;i<left.Length;i++)
            {
                int source=(i-offset+left.Length)%left.Length;
                smoothL+=0.27*(dryL[source]-smoothL);smoothR+=0.27*(dryR[source]-smoothR);
                left[i]+=(float)(smoothR*gain);right[i]+=(float)(smoothL*gain);
            }
        }
        // Ease only the final 3 ms into the first sample to eliminate a boundary step.
        int seam=Rate*3/1000;
        double seamL=left[0]-left[left.Length-1],seamR=right[0]-right[right.Length-1];
        for(int i=0;i<seam;i++)
        {
            double weight=0.5-0.5*Math.Cos(Math.PI*i/(seam-1));
            int at=left.Length-seam+i;
            left[at]+=(float)(seamL*weight);right[at]+=(float)(seamR*weight);
        }
        double peak=0,square=0;
        for(int i=0;i<left.Length;i++) peak=Math.Max(peak,Math.Max(Math.Abs(left[i]),Math.Abs(right[i])));
        double scale=Math.Pow(10,-2.0/20)/peak;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+left.Length*4);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
            writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);
            writer.Write((short)4);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(left.Length*4);
            for(int i=0;i<left.Length;i++)
            {
                double l=left[i]*scale,r=right[i]*scale;
                square+=l*l+r*r;writer.Write((short)Math.Round(l*32767));writer.Write((short)Math.Round(r*32767));
            }
        }
        Console.WriteLine("Created: "+Path.GetFullPath(path));
        Console.WriteLine("60 s | 128 BPM | 32 bars | 44100 Hz stereo PCM16 | "+voices+" sampled notes");
        Console.WriteLine("Peak -2 dBFS | RMS "+(20*Math.Log10(Math.Sqrt(square/(left.Length*2)))).ToString("F2")+" dBFS");
        Console.WriteLine("Loop delta L/R: "+(Math.Abs(left[0]-left[left.Length-1])*scale).ToString("F6")+" / "+(Math.Abs(right[0]-right[right.Length-1])*scale).ToString("F6"));
    }
}
