namespace Frost.Core;

// RIFF jiffies are 1/60 s. UI motion preferences do not affect functional cursors.
public sealed record AniTimeline(int[] Sequence,uint[] Jiffies)
{
    public double DurationMilliseconds=>Jiffies.Sum(j=>(double)j)*1000/60;
    public int StepAt(double milliseconds)
    {
        double ticks=(milliseconds%DurationMilliseconds)*60/1000;
        for(int i=0;i<Jiffies.Length;i++){if(ticks<Jiffies[i])return i;ticks-=Jiffies[i];}
        return 0;
    }
    public static AniTimeline Read(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<12||!bytes[..4].SequenceEqual("RIFF"u8)||BitConverter.ToUInt32(bytes[4..])!=bytes.Length-8||!bytes.Slice(8,4).SequenceEqual("ACON"u8))throw new InvalidDataException("Invalid ANI RIFF header.");
        int frames=0,steps=0;uint defaultRate=0,flags=0;uint[]? rates=null;int[]? sequence=null;bool header=false;
        for(int i=12;i<bytes.Length;)
        {
            if(i+8>bytes.Length)throw new InvalidDataException("Truncated ANI chunk.");
            uint size=BitConverter.ToUInt32(bytes[(i+4)..]);long end=(long)i+8+size;
            if(end>bytes.Length)throw new InvalidDataException("Truncated ANI chunk.");
            var data=bytes.Slice(i+8,(int)size);var id=bytes.Slice(i,4);
            if(id.SequenceEqual("anih"u8))
            {
                if(header||size!=36||BitConverter.ToUInt32(data)!=36)throw new InvalidDataException("Invalid ANI header.");
                uint frameCount=BitConverter.ToUInt32(data[4..]),stepCount=BitConverter.ToUInt32(data[8..]);
                if(frameCount is <1 or >256||stepCount is <1 or >4096)throw new InvalidDataException("ANI resource limits exceeded.");
                header=true;frames=(int)frameCount;steps=(int)stepCount;defaultRate=BitConverter.ToUInt32(data[28..]);flags=BitConverter.ToUInt32(data[32..]);
                if(frames is <1 or >256||steps is <1 or >4096||defaultRate==0||(flags&1)==0||(flags&~3u)!=0)throw new InvalidDataException("Unsupported ANI frames, timing or flags.");
            }
            if(id.SequenceEqual("rate"u8)||id.SequenceEqual("seq "u8))
            {
                if(size%4!=0||size>4096*4)throw new InvalidDataException("Invalid ANI timing/sequence size.");
                var values=new uint[size/4];for(int j=0;j<values.Length;j++)values[j]=BitConverter.ToUInt32(data[(j*4)..]);
                if(id.SequenceEqual("rate"u8)){if(rates!=null)throw new InvalidDataException("Duplicate ANI rates.");rates=values;}
                else{if(sequence!=null||values.Any(v=>v>255))throw new InvalidDataException("Invalid ANI sequence.");sequence=values.Select(v=>(int)v).ToArray();}
            }
            i=checked((int)end+((int)size&1));if(i>bytes.Length)throw new InvalidDataException("Missing ANI padding.");
        }
        if(!header||rates!=null&&(rates.Length!=steps||rates.Any(r=>r==0))||sequence!=null&&(sequence.Length!=steps||sequence.Any(f=>f<0||f>=frames))||(flags&2)!=0&&sequence==null||(flags&2)==0&&steps>frames)throw new InvalidDataException("Invalid ANI sequence or timing.");
        return new((flags&2)!=0?sequence!:Enumerable.Range(0,steps).ToArray(),rates??Enumerable.Repeat(defaultRate,steps).ToArray());
    }
}
