namespace XPScript.Compiler;

internal static class NotesRuntimeRichTextObjectsSource
{
    public const string Code = """
internal static class XPScriptNotesRichTextConstants
{
    internal const int StyleNoChange = 255;
    internal const int RtElemTable = 1;
    internal const int RtElemTextRun = 3;
    internal const int RtElemTextParagraph = 4;
    internal const int RtElemDocLink = 5;
    internal const int RtElemSection = 6;
    internal const int RtElemTableCell = 7;
    internal const int RtElemFileAttachment = 8;
    internal const int RtElemOle = 9;
    internal const int RtElemTextPosition = 10;
    internal const int RtElemTextString = 11;

    internal const ushort SigParagraph = 129;
    internal const ushort SigText = 0xff85;
    internal const ushort SigLink2 = 0xff91;
    internal const ushort SigLinkExport2 = 0xff92;
    internal const ushort SigTableBegin = 163;
    internal const ushort SigTableCell = 164;
    internal const ushort SigTableEnd = 165;
    internal const ushort SigOleBegin = 0xffa7;
    internal const ushort SigHotspotBegin = 0xffa9;
    internal const ushort SigBar = 0xffac;
    internal const ushort SigV4HotspotBegin = 0xffad;
    internal const ushort SigV5HotspotBegin = 0xff7e;
    internal const ushort SigTableLabel = 0xffe3;

    internal const int BarExpanded = 2;
    internal const int BarIsFormula = 0x2000;
    internal const int BarHasColor = 0x04000000;
    internal const ushort TableBidiRtl = 0x0010;
}

internal sealed class XPScriptNotesColorObject : XPScriptNotesObject
{
    private int _notesColor;
    private int _red;
    private int _green;
    private int _blue;

    internal XPScriptNotesColorObject(XPScriptNotesSession session, int color = 0) : base(session) { NotesColor = color; }

    public int NotesColor
    {
        get { EnsureAlive(); return _notesColor; }
        set
        {
            EnsureAlive();
            _notesColor = Math.Clamp(value, 0, 240);
            var rgb = Palette(_notesColor);
            _red = rgb[0]; _green = rgb[1]; _blue = rgb[2];
        }
    }
    public int Red { get { EnsureAlive(); return _red; } }
    public int Green { get { EnsureAlive(); return _green; } }
    public int Blue { get { EnsureAlive(); return _blue; } }
    public int Hue { get { EnsureAlive(); return ToHsl()[0]; } }
    public int Saturation { get { EnsureAlive(); return ToHsl()[1]; } }
    public int Luminance { get { EnsureAlive(); return ToHsl()[2]; } }

    public void SetRGB(object? redValue, object? greenValue, object? blueValue)
    {
        EnsureAlive();
        _red = Math.Clamp(XPScriptRuntime.CInt(redValue), 0, 255);
        _green = Math.Clamp(XPScriptRuntime.CInt(greenValue), 0, 255);
        _blue = Math.Clamp(XPScriptRuntime.CInt(blueValue), 0, 255);
        _notesColor = NearestPalette(_red, _green, _blue);
    }

    public void SetHSL(object? hueValue, object? saturationValue, object? luminanceValue)
    {
        EnsureAlive();
        var h = Math.Clamp(XPScriptRuntime.CInt(hueValue), 0, 240) / 240.0;
        var s = Math.Clamp(XPScriptRuntime.CInt(saturationValue), 0, 240) / 240.0;
        var l = Math.Clamp(XPScriptRuntime.CInt(luminanceValue), 0, 240) / 240.0;
        double r, g, b;
        if (s == 0) r = g = b = l;
        else
        {
            var q = l < .5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueRgb(p, q, h + 1.0 / 3.0); g = HueRgb(p, q, h); b = HueRgb(p, q, h - 1.0 / 3.0);
        }
        SetRGB((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
    }

    private int[] ToHsl()
    {
        var r = _red / 255.0; var g = _green / 255.0; var b = _blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
        var h = 0.0; var s = 0.0; var l = (max + min) / 2.0;
        if (max != min)
        {
            var d = max - min; s = l > .5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0); else if (max == g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
            h /= 6;
        }
        return [(int)Math.Round(h * 240), (int)Math.Round(s * 240), (int)Math.Round(l * 240)];
    }
    private static int[] Palette(int color) => color switch
    {
        0 => [0,0,0], 1 => [255,255,255], 2 => [255,0,0], 3 => [0,255,0], 4 => [0,0,255], 5 => [255,255,0], 6 => [255,0,255], 7 => [0,255,255],
        8 => [128,0,0], 9 => [0,128,0], 10 => [0,0,128], 11 => [128,128,0], 12 => [128,0,128], 13 => [0,128,128], 14 => [128,128,128], 15 => [192,192,192],
        _ => [color,color,color]
    };
    private static int NearestPalette(int r, int g, int b)
    {
        var best = 0; var distance = long.MaxValue;
        for (var i = 0; i <= 15; i++) { var c = Palette(i); var dr=r-c[0]; var dg=g-c[1]; var db=b-c[2]; var d=(long)dr*dr+(long)dg*dg+(long)db*db; if(d<distance){distance=d;best=i;} }
        return best;
    }
    private static double HueRgb(double p,double q,double t){if(t<0)t+=1;if(t>1)t-=1;if(t<1.0/6)return p+(q-p)*6*t;if(t<.5)return q;if(t<2.0/3)return p+(q-p)*(2.0/3-t)*6;return p;}
    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesRichTextStyle : XPScriptNotesObject
{
    internal XPScriptNotesRichTextStyle(XPScriptNotesSession session) : base(session) { }
    public object Bold { get; set; } = 255;
    public int Effects { get; set; } = 255;
    public int FontSize { get; set; } = 255;
    public object Italic { get; set; } = 255;
    public int NotesColor { get; set; } = 255;
    public int NotesFont { get; set; } = 255;
    public bool PassThruHTML { get; set; }
    public object Strikethrough { get; set; } = 255;
    public object Underline { get; set; } = 255;
    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public bool IsDefault => IsNoChange(Bold) && Effects==255 && FontSize==255 && IsNoChange(Italic) && NotesColor==255 && NotesFont==255 && !PassThruHTML && IsNoChange(Strikethrough) && IsNoChange(Underline);

    internal uint ApplyToFontId(uint value)
    {
        var face = NotesFont==255 ? (byte)(value&0xff) : (byte)Math.Clamp(NotesFont,0,255);
        var attr = (byte)((value>>8)&0xff);
        var color = NotesColor==255 ? (byte)((value>>16)&0xff) : (byte)Math.Clamp(NotesColor,0,255);
        var size = FontSize==255 ? (byte)((value>>24)&0xff) : (byte)Math.Clamp(FontSize,0,255); if(size==0) size=10;
        attr=ApplyFlag(attr,1,Bold); attr=ApplyFlag(attr,2,Italic); attr=ApplyFlag(attr,4,Underline); attr=ApplyFlag(attr,8,Strikethrough);
        return (uint)(face|(attr<<8)|(color<<16)|(size<<24));
    }
    internal static XPScriptNotesRichTextStyle FromFontId(XPScriptNotesSession session,uint value)=>new(session){NotesFont=(byte)(value&0xff),NotesColor=(byte)((value>>16)&0xff),FontSize=(byte)((value>>24)&0xff),Bold=((value>>8)&1)!=0,Italic=((value>>8)&2)!=0,Underline=((value>>8)&4)!=0,Strikethrough=((value>>8)&8)!=0,Effects=0};
    internal XPScriptNotesRichTextStyle Copy()=>new(Session){Bold=Bold,Effects=Effects,FontSize=FontSize,Italic=Italic,NotesColor=NotesColor,NotesFont=NotesFont,PassThruHTML=PassThruHTML,Strikethrough=Strikethrough,Underline=Underline};
    internal void Overlay(XPScriptNotesRichTextStyle s){if(!IsNoChange(s.Bold))Bold=s.Bold;if(s.Effects!=255)Effects=s.Effects;if(s.FontSize!=255)FontSize=s.FontSize;if(!IsNoChange(s.Italic))Italic=s.Italic;if(s.NotesColor!=255)NotesColor=s.NotesColor;if(s.NotesFont!=255)NotesFont=s.NotesFont;if(!IsNoChange(s.Strikethrough))Strikethrough=s.Strikethrough;if(!IsNoChange(s.Underline))Underline=s.Underline;PassThruHTML=s.PassThruHTML;}
    private static bool IsNoChange(object? value)=>value is not bool && XPScriptRuntime.CInt(value)==255;
    private static byte ApplyFlag(byte value,byte flag,object setting)=>IsNoChange(setting)?value:(XPScriptRuntime.CBool(setting)?(byte)(value|flag):(byte)(value&~flag));
    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesRichTextParagraphStyle : XPScriptNotesObject
{
    private readonly List<XPScriptNotesRichTextTab> _tabs=[];
    internal XPScriptNotesRichTextParagraphStyle(XPScriptNotesSession session):base(session){ }
    public int Alignment { get; set; }
    public int FirstLineLeftMargin { get; set; }
    public int InterLineSpacing { get; set; }
    public int LeftMargin { get; set; }
    public int Pagination { get; set; }
    public int RightMargin { get; set; }
    public int SpacingAbove { get; set; }
    public int SpacingBelow { get; set; }
    public LSArray Tabs { get { EnsureAlive(); if(_tabs.Count==0)return new LSArray("Variant",true);var a=new LSArray("Variant",true,[0],[_tabs.Count-1]);for(var i=0;i<_tabs.Count;i++)a.Set(_tabs[i],i);return a;} }
    public void ClearAllTabs(){EnsureAlive();foreach(var tab in _tabs.ToArray())tab.Recycle();_tabs.Clear();}
    public void SetTab(object? positionValue,object? typeValue){EnsureAlive();var p=XPScriptRuntime.CInt(positionValue);var t=XPScriptRuntime.CInt(typeValue);if(p<0||t<0||t>3)throw new XPScriptRuntimeException(5,"Invalid rich text tab.");var old=_tabs.FirstOrDefault(x=>x.Position==p);if(old is not null){old.SetType(t);return;}_tabs.Add(new XPScriptNotesRichTextTab(Session,this,p,t));_tabs.Sort((a,b)=>a.Position.CompareTo(b.Position));}
    public void SetTabs(object? countValue,object? startValue,object? intervalValue)=>SetTabs(countValue,startValue,intervalValue,0);
    public void SetTabs(object? countValue,object? startValue,object? intervalValue,object? typeValue){var count=XPScriptRuntime.CInt(countValue);var start=XPScriptRuntime.CInt(startValue);var interval=XPScriptRuntime.CInt(intervalValue);if(count<0||count>20||interval<0)throw new XPScriptRuntimeException(5,"Invalid rich text tab range.");ClearAllTabs();for(var i=0;i<count;i++)SetTab(start+i*interval,typeValue);}
    internal short[] TabPositions()=>_tabs.Take(20).Select(tab=>checked((short)Math.Clamp(tab.Position,0,ushort.MaxValue))).ToArray();
    internal void RemoveTab(XPScriptNotesRichTextTab tab)=>_tabs.Remove(tab);
    protected override void ReleaseNative()=>ClearAllTabs();
}

internal sealed class XPScriptNotesRichTextTab : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextParagraphStyle _owner; private bool _cleared;
    internal XPScriptNotesRichTextTab(XPScriptNotesSession session,XPScriptNotesRichTextParagraphStyle owner,int position,int type):base(session){_owner=owner;Position=position;Type=type;}
    public int Position { get; private set; }
    public int Type { get; private set; }
    internal void SetType(int type)=>Type=type;
    public void Clear(){EnsureAlive();if(!_cleared){_owner.RemoveTab(this);_cleared=true;}}
    protected override void ReleaseNative(){if(!_cleared){_owner.RemoveTab(this);_cleared=true;}}
}

internal sealed class XPScriptNotesRichTextRecord
{
    internal XPScriptNotesRichTextRecord(int itemOrdinal,ushort signature,byte[] data){ItemOrdinal=itemOrdinal;Signature=signature;Data=data;}
    internal int ItemOrdinal { get; }
    internal ushort Signature { get; }
    internal byte[] Data { get; }
    internal XPScriptNotesRichTextRecord Copy()=>new(ItemOrdinal,Signature,(byte[])Data.Clone());
}

internal sealed class XPScriptNotesRichTextElementInfo
{
    internal XPScriptNotesRichTextElementInfo(int type,int start,int end,int offset=0){Type=type;StartRecord=start;EndRecord=end;CharOffset=offset;}
    internal int Type { get; }
    internal int StartRecord { get; }
    internal int EndRecord { get; }
    internal int CharOffset { get; }
}

internal static class XPScriptNotesRichTextModel
{
    internal static List<XPScriptNotesRichTextElementInfo> Elements(IReadOnlyList<XPScriptNotesRichTextRecord> records,int min=0,int max=int.MaxValue)
    {
        var result=new List<XPScriptNotesRichTextElementInfo>();max=Math.Min(max,records.Count-1);
        for(var i=Math.Max(0,min);i<=max;i++)
        {
            var sig=records[i].Signature;
            if(sig==XPScriptNotesRichTextConstants.SigTableBegin){var depth=1;var end=i;for(var j=i+1;j<=max;j++){if(records[j].Signature==XPScriptNotesRichTextConstants.SigTableBegin)depth++;else if(records[j].Signature==XPScriptNotesRichTextConstants.SigTableEnd&&--depth==0){end=j;break;}}result.Add(new(1,i,end));}
            else if(sig==XPScriptNotesRichTextConstants.SigTableCell){var end=i;for(var j=i+1;j<=max;j++){if(records[j].Signature is XPScriptNotesRichTextConstants.SigTableCell or XPScriptNotesRichTextConstants.SigTableEnd)break;end=j;}result.Add(new(7,i,end));}
            else if(sig==XPScriptNotesRichTextConstants.SigBar)result.Add(new(6,i,i));
            else if(sig is XPScriptNotesRichTextConstants.SigLink2 or XPScriptNotesRichTextConstants.SigLinkExport2)result.Add(new(5,i,i));
            else if(sig==XPScriptNotesRichTextConstants.SigText)result.Add(new(3,i,i));
            else if(sig==XPScriptNotesRichTextConstants.SigParagraph){var end=i;for(var j=i+1;j<=max;j++){if(records[j].Signature==XPScriptNotesRichTextConstants.SigParagraph)break;end=j;}result.Add(new(4,i,end));}
            else if(sig==XPScriptNotesRichTextConstants.SigOleBegin)result.Add(new(9,i,i));
            else if(IsFileHotspot(records[i]))result.Add(new(8,i,i));
        }
        return result;
    }
    internal static bool IsFileHotspot(XPScriptNotesRichTextRecord r)=>r.Signature is XPScriptNotesRichTextConstants.SigHotspotBegin or XPScriptNotesRichTextConstants.SigV4HotspotBegin or XPScriptNotesRichTextConstants.SigV5HotspotBegin && r.Data.Length>=6 && U16(r.Data,4)==4;
    internal static string Text(XPScriptNotesRichTextItem item,IReadOnlyList<XPScriptNotesRichTextRecord> records,int start,int end){var parts=new List<string>();for(var i=Math.Max(0,start);i<=Math.Min(end,records.Count-1);i++){var r=records[i];if(r.Signature==XPScriptNotesRichTextConstants.SigText&&r.Data.Length>8)parts.Add(item.DecodeRichTextBytes(r.Data.AsSpan(8).ToArray()));}return string.Concat(parts);}
    internal static ushort U16(byte[] data,int offset)=>offset+2<=data.Length?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset,2)):(ushort)0;
    internal static uint U32(byte[] data,int offset)=>offset+4<=data.Length?System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset,4)):0u;
    internal static void W16(byte[] data,int offset,ushort value){if(offset+2<=data.Length)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset,2),value);}
    internal static void W32(byte[] data,int offset,uint value){if(offset+4<=data.Length)System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset,4),value);}
}

internal sealed class XPScriptNotesRichTextNavigator : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextItem _item; private readonly int _min; private readonly int _max; private int _record=-1; private int _end=-1; private int _type; private int _offset; private int _lastType;
    internal XPScriptNotesRichTextNavigator(XPScriptNotesRichTextItem item,int min=0,int max=int.MaxValue):base(item.RichTextSession){_item=item;_min=min;_max=max;}
    internal XPScriptNotesRichTextItem RichTextItem=>_item; internal int CurrentRecord=>_record; internal int CurrentEndRecord=>_end; internal int CurrentType=>_type; internal int CurrentCharOffset=>_offset;
    public XPScriptNotesRichTextNavigator Clone()=>new(_item,_min,_max){_record=_record,_end=_end,_type=_type,_offset=_offset,_lastType=_lastType};
    public bool FindFirstElement(object? typeValue)=>FindNthElement(typeValue,1);
    public bool FindLastElement(object? typeValue){var type=ValidType(typeValue);var e=All().Where(x=>x.Type==type).LastOrDefault();if(e is null)return false;Set(e);_lastType=type;return true;}
    public bool FindNthElement(object? typeValue,object? occurrenceValue){var type=ValidType(typeValue);var n=XPScriptRuntime.CInt(occurrenceValue);if(n<=0)throw new XPScriptRuntimeException(5,"Rich text occurrence must be positive.");var e=All().Where(x=>x.Type==type).Skip(n-1).FirstOrDefault();if(e is null)return false;Set(e);_lastType=type;return true;}
    public bool FindNextElement()=>FindNextElement(_lastType,1);
    public bool FindNextElement(object? typeValue)=>FindNextElement(typeValue,1);
    public bool FindNextElement(object? typeValue,object? occurrenceValue){if(_record<0)return false;var type=ValidType(typeValue);var n=XPScriptRuntime.CInt(occurrenceValue);if(n<=0)throw new XPScriptRuntimeException(5,"Rich text occurrence must be positive.");var e=All().Where(x=>x.Type==type&&x.StartRecord>_record).Skip(n-1).FirstOrDefault();if(e is null)return false;Set(e);_lastType=type;return true;}
    public bool FindFirstString(object? targetValue)=>FindFirstString(targetValue,0);
    public bool FindFirstString(object? targetValue,object? optionsValue)=>FindString(targetValue,optionsValue,false);
    public bool FindNextString(object? targetValue)=>FindNextString(targetValue,0);
    public bool FindNextString(object? targetValue,object? optionsValue)=>FindString(targetValue,optionsValue,true);
    public object? GetElement(){if(_record<0)throw new XPScriptRuntimeException(5,"NotesRichTextNavigator has no current position.");return Materialize();}
    public object? GetFirstElement(object? t){return FindFirstElement(t)?Materialize():null;}
    public object? GetLastElement(object? t){return FindLastElement(t)?Materialize():null;}
    public object? GetNthElement(object? t,object? n){return FindNthElement(t,n)?Materialize():null;}
    public object? GetNextElement(){return FindNextElement()?Materialize():null;}
    public object? GetNextElement(object? t){return FindNextElement(t)?Materialize():null;}
    public object? GetNextElement(object? t,object? n){return FindNextElement(t,n)?Materialize():null;}
    public void SetCharOffset(object? value){var v=XPScriptRuntime.CInt(value);if(v<0||_record<0)throw new XPScriptRuntimeException(5,"Invalid rich text character offset.");_offset+=v;_type=10;}
    public void SetPosition(object? value)=>Position(value,false);
    public void SetPositionAtEnd(object? value)=>Position(value,true);
    private bool FindString(object? targetValue,object? optionsValue,bool next){var target=XPScriptRuntime.CStr(targetValue);if(target.Length==0)return false;var cmp=(XPScriptRuntime.CInt(optionsValue)&1)!=0?StringComparison.CurrentCultureIgnoreCase:StringComparison.CurrentCulture;var records=_item.ReadRichTextRecords();var start=Math.Max(_min,next&&_record>=0?_record:_min);for(var i=start;i<records.Count&&i<=_max;i++){var r=records[i];if(r.Signature!=XPScriptNotesRichTextConstants.SigText||r.Data.Length<=8)continue;var text=_item.DecodeRichTextBytes(r.Data.AsSpan(8).ToArray());var from=next&&i==_record?Math.Min(text.Length,_offset+1):0;var p=text.IndexOf(target,from,cmp);if(p>=0){_record=_end=i;_type=11;_offset=p;return true;}}return false;}
    private object? Materialize(){if(_type is 3 or 4 or 7 or 10 or 11)throw new XPScriptRuntimeException(5,"This rich text element must be accessed through NotesRichTextRange.");return _type switch{1=>new XPScriptNotesRichTextTable(_item,_record,_end),5=>new XPScriptNotesRichTextDocLink(_item,_record,_end),6=>new XPScriptNotesRichTextSection(_item,_record,_end),8 or 9=>null,_=>null};}
    private void Position(object? value,bool end){switch(value){case XPScriptNotesRichTextNavigator n when ReferenceEquals(n._item,_item):_record=end?n._end:n._record;_end=n._end;_type=n._type;_offset=end?int.MaxValue:n._offset;break;case XPScriptNotesRichTextRange r when ReferenceEquals(r.RichTextItem,_item):_record=end?r.EndRecord:r.BeginRecord;_end=r.EndRecord;_type=r.Type;_offset=end?int.MaxValue:0;break;case XPScriptNotesRichTextElementObject e when ReferenceEquals(e.RichTextItem,_item):_record=end?e.EndRecord:e.StartRecord;_end=e.EndRecord;_type=e.ElementType;_offset=end?int.MaxValue:0;break;default:throw new XPScriptRuntimeException(13,"SetPosition requires an element from the same rich text item.");}}
    private List<XPScriptNotesRichTextElementInfo> All()=>XPScriptNotesRichTextModel.Elements(_item.ReadRichTextRecords(),_min,_max);
    private void Set(XPScriptNotesRichTextElementInfo e){_record=e.StartRecord;_end=e.EndRecord;_type=e.Type;_offset=e.CharOffset;}
    private static int ValidType(object? value){var t=XPScriptRuntime.CInt(value);if(t is not(1 or 3 or 4 or 5 or 6 or 7 or 8 or 9))throw new XPScriptRuntimeException(5,"Unsupported rich text element type.");return t;}
    protected override void ReleaseNative(){ }
}

internal sealed class XPScriptNotesRichTextRange : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextItem _item; private int _begin=-1; private int _end=-1; private int _type; private int _offset;
    internal XPScriptNotesRichTextRange(XPScriptNotesRichTextItem item):base(item.RichTextSession){_item=item;}
    internal XPScriptNotesRichTextItem RichTextItem=>_item; internal int BeginRecord=>_begin<0?0:_begin; internal int EndRecord=>_end<0?Math.Max(0,_item.ReadRichTextRecords().Count-1):_end;
    public int Type=>_type;
    public XPScriptNotesRichTextNavigator Navigator=>new(_item,BeginRecord,EndRecord);
    public XPScriptNotesRichTextStyle Style{get{var records=_item.ReadRichTextRecords();for(var i=BeginRecord;i<=Math.Min(EndRecord,records.Count-1);i++)if(records[i].Signature==XPScriptNotesRichTextConstants.SigText&&records[i].Data.Length>=8)return XPScriptNotesRichTextStyle.FromFontId(Session,XPScriptNotesRichTextModel.U32(records[i].Data,4));return new XPScriptNotesRichTextStyle(Session);}}
    public string TextRun{get{if(_begin<0)return "";var records=_item.ReadRichTextRecords();if(_type is 3 or 10 or 11){var s=XPScriptNotesRichTextModel.Text(_item,records,_begin,_begin);return _offset>0&&_offset<s.Length?s[_offset..]:s;}var run=XPScriptNotesRichTextModel.Elements(records,BeginRecord,EndRecord).FirstOrDefault(e=>e.Type==3);return run is null?"":XPScriptNotesRichTextModel.Text(_item,records,run.StartRecord,run.EndRecord);}}
    public string TextParagraph{get{if(_begin<0)return "";var records=_item.ReadRichTextRecords();if(_type==4)return XPScriptNotesRichTextModel.Text(_item,records,BeginRecord,EndRecord);var p=XPScriptNotesRichTextModel.Elements(records,BeginRecord,EndRecord).FirstOrDefault(e=>e.Type==4);return p is null?TextRun:XPScriptNotesRichTextModel.Text(_item,records,p.StartRecord,p.EndRecord);}}
    public XPScriptNotesRichTextRange Clone()=>new(_item){_begin=_begin,_end=_end,_type=_type,_offset=_offset};
    public void Reset(){_begin=-1;_end=-1;_type=0;_offset=0;}
    public void SetBegin(object? value)=>Boundary(value,true);
    public void SetEnd(object? value)=>Boundary(value,false);
    public int FindAndReplace(object? target,object? replacement)=>FindAndReplace(target,replacement,0);
    public int FindAndReplace(object? targetValue,object? replacementValue,object? optionsValue){var target=XPScriptRuntime.CStr(targetValue);if(target.Length==0)return 0;var replacement=XPScriptRuntime.CStr(replacementValue);var options=XPScriptRuntime.CInt(optionsValue);var all=(options&16)!=0;var cmp=(options&1)!=0?StringComparison.CurrentCultureIgnoreCase:StringComparison.CurrentCulture;var records=_item.ReadRichTextRecords().Select(r=>r.Copy()).ToList();var count=0;for(var i=BeginRecord;i<=Math.Min(EndRecord,records.Count-1);i++){var r=records[i];if(r.Signature!=XPScriptNotesRichTextConstants.SigText||r.Data.Length<=8)continue;var text=_item.DecodeRichTextBytes(r.Data.AsSpan(8).ToArray());var p=text.IndexOf(target,cmp);while(p>=0){text=text[..p]+replacement+text[(p+target.Length)..];count++;if(!all)break;p=text.IndexOf(target,p+replacement.Length,cmp);}if(count>0){var encoded=_item.EncodeRichTextText(text);var data=new byte[8+encoded.Length];Array.Copy(r.Data,0,data,0,8);Array.Copy(encoded,0,data,8,encoded.Length);XPScriptNotesRichTextModel.W16(data,2,checked((ushort)data.Length));records[i]=new(r.ItemOrdinal,r.Signature,data);if(!all)break;}}if(count>0)_item.ReplaceRichTextRecords(records);Reset();return count;}
    public void Remove(){if(_begin<0)return;var records=_item.ReadRichTextRecords().Select(r=>r.Copy()).ToList();var start=Math.Clamp(BeginRecord,0,records.Count);var end=Math.Clamp(EndRecord,start-1,records.Count-1);if(end>=start)records.RemoveRange(start,end-start+1);_item.ReplaceRichTextRecords(records);Reset();}
    public void SetStyle(object? value){if(value is not XPScriptNotesRichTextStyle style)throw new XPScriptRuntimeException(13,"SetStyle requires a NotesRichTextStyle.");var records=_item.ReadRichTextRecords().Select(r=>r.Copy()).ToList();for(var i=BeginRecord;i<=Math.Min(EndRecord,records.Count-1);i++)if(records[i].Signature==XPScriptNotesRichTextConstants.SigText&&records[i].Data.Length>=8)XPScriptNotesRichTextModel.W32(records[i].Data,4,style.ApplyToFontId(XPScriptNotesRichTextModel.U32(records[i].Data,4)));_item.ReplaceRichTextRecords(records);}
    private void Boundary(object? value,bool begin){int s,e,t,o=0;switch(value){case XPScriptNotesRichTextNavigator n when ReferenceEquals(n.RichTextItem,_item):s=n.CurrentRecord;e=n.CurrentEndRecord;t=n.CurrentType;o=n.CurrentCharOffset;break;case XPScriptNotesRichTextRange r when ReferenceEquals(r._item,_item):s=r.BeginRecord;e=r.EndRecord;t=r.Type;break;case XPScriptNotesRichTextElementObject x when ReferenceEquals(x.RichTextItem,_item):s=x.StartRecord;e=x.EndRecord;t=x.ElementType;break;default:throw new XPScriptRuntimeException(13,"Range boundary requires an element from the same rich text item.");}if(s<0)throw new XPScriptRuntimeException(5,"The supplied rich text object has no current position.");if(begin){_begin=s;_offset=o;if(_end<0)_end=e;_type=t;}else{_end=e;if(_begin<0){_begin=s;_type=t;}}if(_end<_begin)(_begin,_end)=(_end,_begin);}
    protected override void ReleaseNative(){ }
}

internal abstract class XPScriptNotesRichTextElementObject : XPScriptNotesObject
{
    protected XPScriptNotesRichTextElementObject(XPScriptNotesRichTextItem item,int type,int start,int end):base(item.RichTextSession){RichTextItem=item;ElementType=type;StartRecord=start;EndRecord=end;}
    internal XPScriptNotesRichTextItem RichTextItem { get; }
    internal int ElementType { get; }
    internal int StartRecord { get; protected set; }
    internal int EndRecord { get; protected set; }
    protected List<XPScriptNotesRichTextRecord> Records()=>RichTextItem.ReadRichTextRecords();
    protected void Rewrite(List<XPScriptNotesRichTextRecord> records)=>RichTextItem.ReplaceRichTextRecords(records);
    public void Remove(){var records=Records().Select(r=>r.Copy()).ToList();if(StartRecord>=0&&StartRecord<records.Count){var end=Math.Min(EndRecord,records.Count-1);records.RemoveRange(StartRecord,end-StartRecord+1);Rewrite(records);}}
    protected override void ReleaseNative(){ }
}

internal sealed class XPScriptNotesRichTextSection : XPScriptNotesRichTextElementObject
{
    internal XPScriptNotesRichTextSection(XPScriptNotesRichTextItem item,int start,int end):base(item,6,start,end){ }
    public bool IsExpanded{get{var r=Bar();return(XPScriptNotesRichTextModel.U32(r.Data,4)&2)!=0;}set=>Update(data=>{var f=XPScriptNotesRichTextModel.U32(data,4);XPScriptNotesRichTextModel.W32(data,4,value?f|2u:f&~2u);});}
    public string Title{get{var r=Bar();if(r.Data.Length<=12)return"";var flags=XPScriptNotesRichTextModel.U32(r.Data,4);if((flags&XPScriptNotesRichTextConstants.BarIsFormula)!=0)return"";var o=12+(((flags&XPScriptNotesRichTextConstants.BarHasColor)!=0)?2:0);return o<r.Data.Length?RichTextItem.DecodeRichTextBytes(r.Data.AsSpan(o).ToArray()).TrimEnd('\0'):"";}}
    public XPScriptNotesRichTextStyle TitleStyle{get{var r=Bar();return r.Data.Length>=12?XPScriptNotesRichTextStyle.FromFontId(RichTextItem.RichTextSession,XPScriptNotesRichTextModel.U32(r.Data,8)):new XPScriptNotesRichTextStyle(RichTextItem.RichTextSession);}}
    public XPScriptNotesColorObject BarColor{get{var r=Bar();var f=XPScriptNotesRichTextModel.U32(r.Data,4);return new XPScriptNotesColorObject(RichTextItem.RichTextSession,(f&XPScriptNotesRichTextConstants.BarHasColor)!=0&&r.Data.Length>=14?XPScriptNotesRichTextModel.U16(r.Data,12):0);}}
    public void SetTitleStyle(object? value){if(value is not XPScriptNotesRichTextStyle s)throw new XPScriptRuntimeException(13,"SetTitleStyle requires a NotesRichTextStyle.");Update(data=>XPScriptNotesRichTextModel.W32(data,8,s.ApplyToFontId(XPScriptNotesRichTextModel.U32(data,8))));}
    public void SetBarColor(object? value){if(value is not XPScriptNotesColorObject c)throw new XPScriptRuntimeException(13,"SetBarColor requires a NotesColorObject.");var records=Records().Select(r=>r.Copy()).ToList();var r=records[StartRecord];var flags=XPScriptNotesRichTextModel.U32(r.Data,4);var old=(flags&XPScriptNotesRichTextConstants.BarHasColor)!=0;var titleOffset=12+(old?2:0);var title=titleOffset<r.Data.Length?r.Data.AsSpan(titleOffset).ToArray():[];flags|=XPScriptNotesRichTextConstants.BarHasColor;var data=new byte[14+title.Length];Array.Copy(r.Data,0,data,0,Math.Min(12,r.Data.Length));XPScriptNotesRichTextModel.W32(data,4,flags);XPScriptNotesRichTextModel.W16(data,12,checked((ushort)c.NotesColor));Array.Copy(title,0,data,14,title.Length);XPScriptNotesRichTextModel.W16(data,2,checked((ushort)data.Length));records[StartRecord]=new(r.ItemOrdinal,r.Signature,data);Rewrite(records);}
    private XPScriptNotesRichTextRecord Bar(){var records=Records();if(StartRecord<0||StartRecord>=records.Count)throw new XPScriptRuntimeException(91,"NotesRichTextSection is no longer valid.");return records[StartRecord];}
    private void Update(Action<byte[]> action){var records=Records().Select(r=>r.Copy()).ToList();action(records[StartRecord].Data);Rewrite(records);}
}

internal sealed class XPScriptNotesRichTextTable : XPScriptNotesRichTextElementObject
{
    internal XPScriptNotesRichTextTable(XPScriptNotesRichTextItem item,int start,int end):base(item,1,start,end){ }
    public int RowCount=>Coordinates().Select(x=>x.row).DefaultIfEmpty(-1).Max()+1;
    public int ColumnCount=>Coordinates().Select(x=>x.column).DefaultIfEmpty(-1).Max()+1;
    public bool RightToLeft{get=>(XPScriptNotesRichTextModel.U16(Begin().Data,12)&XPScriptNotesRichTextConstants.TableBidiRtl)!=0;set=>UpdateBegin(f=>value?(ushort)(f|XPScriptNotesRichTextConstants.TableBidiRtl):(ushort)(f&~XPScriptNotesRichTextConstants.TableBidiRtl));}
    public int Style{get{var f=XPScriptNotesRichTextModel.U16(Begin().Data,12);if((f&0x8000)!=0)return 8;if((f&0x4000)!=0)return 7;if((f&0x2000)!=0)return 6;if((f&0x0800)!=0)return 5;if((f&0x0400)!=0)return 4;if((f&0x0200)!=0)return 3;if((f&0x0100)!=0)return 2;if((f&0x0080)!=0)return 1;return 0;}set{ushort bit=value switch{0=>0,1=>0x0080,2=>0x0100,3=>0x0200,4=>0x0400,5=>0x0800,6=>0x2000,7=>0x4000,8=>0x8000,_=>throw new XPScriptRuntimeException(5,"Invalid table style.")};UpdateBegin(f=>(ushort)((f&~0xef80)|bit));}}
    public XPScriptNotesColorObject Color=>new(RichTextItem.RichTextSession,CellColors().FirstOrDefault(0));
    public XPScriptNotesColorObject AlternateColor{get{var c=CellColors().Distinct().ToList();return new XPScriptNotesColorObject(RichTextItem.RichTextSession,c.Count>1?c[1]:15);}}
    public LSArray RowLabels{get{var labels=Labels();var n=RowCount;if(n<=0)return new LSArray("String",true);var a=new LSArray("String",true,[0],[n-1]);for(var i=0;i<n;i++)a.Set(i<labels.Count?labels[i]:"",i);return a;}set=>WriteLabels(value);}
    public void SetColor(object? value){if(value is not XPScriptNotesColorObject c)throw new XPScriptRuntimeException(13,"SetColor requires a NotesColorObject.");SetCellColor(c.NotesColor);}
    public void SetAlternateColor(object? value){if(value is not XPScriptNotesColorObject c)throw new XPScriptRuntimeException(13,"SetAlternateColor requires a NotesColorObject.");SetCellColor(c.NotesColor,true);}
    public void AddRow()=>AddRow(1,RowCount);
    public void AddRow(object? countValue)=>AddRow(countValue,RowCount);
    public void AddRow(object? countValue,object? targetRowValue){var count=XPScriptRuntime.CInt(countValue);var target=XPScriptRuntime.CInt(targetRowValue);if(count<=0||target<0||target>RowCount)throw new XPScriptRuntimeException(5,"Invalid row insertion.");var records=Records().Select(r=>r.Copy()).ToList();var insert=EndRecord;for(var i=StartRecord+1;i<EndRecord;i++)if(records[i].Signature==XPScriptNotesRichTextConstants.SigTableCell&&records[i].Data.Length>3&&records[i].Data[2]>=target){insert=i;break;}for(var i=StartRecord+1;i<EndRecord;i++)if(records[i].Signature==XPScriptNotesRichTextConstants.SigTableCell&&records[i].Data.Length>3&&records[i].Data[2]>=target)records[i].Data[2]=checked((byte)(records[i].Data[2]+count));var add=new List<XPScriptNotesRichTextRecord>();for(var r=0;r<count;r++)for(var c=0;c<ColumnCount;c++){var d=new byte[18];d[0]=164;d[1]=18;d[2]=checked((byte)(target+r));d[3]=checked((byte)c);add.Add(new(0,164,d));}records.InsertRange(insert,add);EndRecord+=add.Count;Rewrite(records);}
    public void RemoveRow()=>RemoveRow(1,RowCount);
    public void RemoveRow(object? countValue)=>RemoveRow(countValue,RowCount);
    public void RemoveRow(object? countValue,object? targetValue){var count=XPScriptRuntime.CInt(countValue);var target=XPScriptRuntime.CInt(targetValue);if(count<=0||target<=0||target>RowCount)throw new XPScriptRuntimeException(5,"Invalid row removal.");var first=target-1;var last=Math.Min(RowCount-1,first+count-1);var records=Records().Select(r=>r.Copy()).ToList();for(var i=EndRecord-1;i>StartRecord;i--)if(records[i].Signature==164&&records[i].Data.Length>3){var row=records[i].Data[2];if(row>=first&&row<=last){records.RemoveAt(i);EndRecord--;}else if(row>last)records[i].Data[2]=checked((byte)(row-(last-first+1)));}Rewrite(records);}
    private List<(int row,int column)> Coordinates(){var a=new List<(int,int)>();var r=Records();for(var i=StartRecord+1;i<Math.Min(EndRecord,r.Count);i++)if(r[i].Signature==164&&r[i].Data.Length>3)a.Add((r[i].Data[2],r[i].Data[3]));return a;}
    private XPScriptNotesRichTextRecord Begin(){var r=Records();if(StartRecord<0||StartRecord>=r.Count)throw new XPScriptRuntimeException(91,"NotesRichTextTable is no longer valid.");return r[StartRecord];}
    private void UpdateBegin(Func<ushort,ushort> f){var r=Records().Select(x=>x.Copy()).ToList();XPScriptNotesRichTextModel.W16(r[StartRecord].Data,12,f(XPScriptNotesRichTextModel.U16(r[StartRecord].Data,12)));Rewrite(r);}
    private List<int> CellColors(){var a=new List<int>();var r=Records();for(var i=StartRecord+1;i<Math.Min(EndRecord,r.Count);i++)if(r[i].Signature==164&&r[i].Data.Length>=18)a.Add(XPScriptNotesRichTextModel.U16(r[i].Data,16));return a;}
    private void SetCellColor(int color,bool alternate=false){var r=Records().Select(x=>x.Copy()).ToList();for(var i=StartRecord+1;i<Math.Min(EndRecord,r.Count);i++)if(r[i].Signature==164&&r[i].Data.Length>=18){var row=r[i].Data[2];var col=r[i].Data[3];var useAlternate=Style switch{4=>(col%2)==0,5=>(row%2)==0,1=>!(row==0||col==0),2=>row!=0,3=>col!=0,6=>!(row==0||col==ColumnCount-1),7=>col!=ColumnCount-1,_=>false};if(useAlternate==alternate)XPScriptNotesRichTextModel.W16(r[i].Data,16,checked((ushort)color));}Rewrite(r);}
    private List<string> Labels(){var a=new List<string>();var r=Records();for(var i=StartRecord+1;i<Math.Min(EndRecord,r.Count);i++)if(r[i].Signature==XPScriptNotesRichTextConstants.SigTableLabel&&r[i].Data.Length>=132){var raw=r[i].Data.AsSpan(4,128).ToArray();var z=Array.IndexOf(raw,(byte)0);if(z>=0)raw=raw[..z];a.Add(RichTextItem.DecodeRichTextBytes(raw));}return a;}
    private void WriteLabels(LSArray value){var labels=new List<string>();if(value.IsAllocated)for(var i=value.LBound(1);i<=value.UBound(1);i++)labels.Add(XPScriptRuntime.CStr(value.Get(i)));var r=Records().Select(x=>x.Copy()).ToList();for(var i=EndRecord-1;i>StartRecord;i--)if(r[i].Signature==XPScriptNotesRichTextConstants.SigTableLabel){r.RemoveAt(i);EndRecord--;}var at=StartRecord+1;for(var i=0;i<Math.Min(labels.Count,RowCount);i++){var d=new byte[140];XPScriptNotesRichTextModel.W16(d,0,XPScriptNotesRichTextConstants.SigTableLabel);XPScriptNotesRichTextModel.W16(d,2,140);var text=RichTextItem.EncodeRichTextText(labels[i]);Array.Copy(text,0,d,4,Math.Min(127,text.Length));XPScriptNotesRichTextModel.W16(d,138,3);r.Insert(at++,new(0,XPScriptNotesRichTextConstants.SigTableLabel,d));EndRecord++;}Rewrite(r);}
}

internal sealed class XPScriptNotesRichTextDocLink : XPScriptNotesRichTextElementObject
{
    internal XPScriptNotesRichTextDocLink(XPScriptNotesRichTextItem item,int start,int end):base(item,5,start,end){ }
    public string DbReplicaID{get=>Hex(4,8);set=>SetHex(4,8,value,16);}
    public string ViewUNID{get=>Hex(12,16);set=>SetHex(12,16,value,32);}
    public string DocUNID{get=>Hex(28,16);set=>SetHex(28,16,value,32);}
    public string DisplayComment{get=>Texts()[0];set=>SetText(0,value);}
    public string ServerHint{get=>Texts()[1];set=>SetText(1,value);}
    public string HotSpotText{get=>Texts()[2];set=>SetText(2,value);}
    public XPScriptNotesRichTextStyle HotSpotTextStyle=>new(RichTextItem.RichTextSession);
    public void SetHotSpotTextStyle(object? value){if(value is not XPScriptNotesRichTextStyle)throw new XPScriptRuntimeException(13,"SetHotSpotTextStyle requires a NotesRichTextStyle.");}
    public void RemoveLinkage(){ }
    private XPScriptNotesRichTextRecord Link(){var r=Records();if(StartRecord<0||StartRecord>=r.Count)throw new XPScriptRuntimeException(91,"NotesRichTextDocLink is no longer valid.");return r[StartRecord];}
    private string Hex(int offset,int length){var r=Link();return offset+length<=r.Data.Length?Convert.ToHexString(r.Data.AsSpan(offset,length)):new string('0',length*2);}
    private void SetHex(int offset,int length,string value,int chars){value=(value??"").Replace("-","",StringComparison.Ordinal).Trim();if(value.Length!=chars)throw new XPScriptRuntimeException(5,"Invalid Notes link identifier.");byte[] b;try{b=Convert.FromHexString(value);}catch{throw new XPScriptRuntimeException(5,"Invalid Notes link identifier.");}var r=Records().Select(x=>x.Copy()).ToList();if(offset+length>r[StartRecord].Data.Length)throw new XPScriptRuntimeException(5,"Doclink linkage cannot be edited.");Array.Copy(b,0,r[StartRecord].Data,offset,length);Rewrite(r);}
    private string[] Texts(){var r=Link();if(r.Signature!=XPScriptNotesRichTextConstants.SigLinkExport2||r.Data.Length<=44)return["","",""];var raw=r.Data.AsSpan(44).ToArray();var result=new string[3];var start=0;for(var i=0;i<3;i++){var end=Array.IndexOf(raw,(byte)0,start);if(end<0)end=raw.Length;result[i]=RichTextItem.DecodeRichTextBytes(raw.AsSpan(start,Math.Max(0,end-start)).ToArray());start=Math.Min(raw.Length,end+1);}return result;}
    private void SetText(int index,string value){var link=Link();if(link.Signature!=XPScriptNotesRichTextConstants.SigLinkExport2)throw new XPScriptRuntimeException(5,"This doclink uses $Links linkage and cannot be rewritten safely.");var t=Texts();t[index]=value??"";var v=new List<byte>();foreach(var s in t){v.AddRange(RichTextItem.EncodeRichTextText(s));v.Add(0);}var d=new byte[44+v.Count];Array.Copy(link.Data,0,d,0,Math.Min(44,link.Data.Length));v.CopyTo(d,44);XPScriptNotesRichTextModel.W16(d,2,checked((ushort)d.Length));var r=Records().Select(x=>x.Copy()).ToList();r[StartRecord]=new(link.ItemOrdinal,link.Signature,d);Rewrite(r);}
}
""";
}
