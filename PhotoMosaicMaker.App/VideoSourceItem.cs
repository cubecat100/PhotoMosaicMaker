using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PhotoMosaicMaker.App
{
    public enum VideoSourceKind
    {
        File = 1,
        Url = 2
    }

    public sealed class VideoSourceItem
    {
        public VideoSourceKind Kind { get; }
        public string Value { get; }

        public string DisplayText
        {
            get
            {
                return Kind == VideoSourceKind.File
                    ? $"[File] {Path.GetFileName(Value)}"
                    : $"[URL] {Value}";
            }
        }

        public VideoSourceItem(VideoSourceKind kind, string value)
        {
            Kind = kind;
            Value = value ?? "";
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
