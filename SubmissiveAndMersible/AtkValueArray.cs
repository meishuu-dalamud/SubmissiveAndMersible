using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using System.Text;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SubmissiveAndMersible
{
    // https://github.com/Limiana/ClickLib/blob/2ba18913e6cba52bc419db95a34dd98a2a8f9f49/ClickLib/Structures/AtkValueArray.cs
    internal unsafe class AtkValueArray : IDisposable
    {
        public AtkValueArray(params object[] values)
        {
            this.Length = values.Length;
            this.Address = Marshal.AllocHGlobal(this.Length * Marshal.SizeOf<AtkValue>());
            this.Pointer = (AtkValue*)this.Address;

            for (var i = 0; i < values.Length; i++)
            {
                this.EncodeValue(i, values[i]);
            }
        }

        public IntPtr Address { get; private set; }

        public AtkValue* Pointer { get; private set; }

        public int Length { get; private set; }

        public static implicit operator AtkValue*(AtkValueArray arr) => arr.Pointer;

        public void Dispose()
        {
            for (var i = 0; i < this.Length; i++)
            {
                if (this.Pointer[i].Type == ValueType.String)
                    Marshal.FreeHGlobal(new IntPtr(this.Pointer[i].String));
            }

            Marshal.FreeHGlobal(this.Address);
        }

        private unsafe void EncodeValue(int index, object value)
        {
            switch (value)
            {
                case uint uintValue:
                    this.Pointer[index].Type = ValueType.UInt;
                    this.Pointer[index].UInt = uintValue;
                    break;
                case int intValue:
                    this.Pointer[index].Type = ValueType.Int;
                    this.Pointer[index].Int = intValue;
                    break;
                case float floatValue:
                    this.Pointer[index].Type = ValueType.Float;
                    this.Pointer[index].Float = floatValue;
                    break;
                case bool boolValue:
                    this.Pointer[index].Type = ValueType.Bool;
                    this.Pointer[index].Byte = Convert.ToByte(boolValue);
                    break;
                case string stringValue:
                    var stringBytes = Encoding.UTF8.GetBytes(stringValue + '\0');
                    var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                    Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length + 1);

                    this.Pointer[index].Type = ValueType.String;
                    this.Pointer[index].String = (byte*)stringAlloc;
                    break;
                default:
                    throw new ArgumentException($"Unable to convert type {value.GetType()} to AtkValue");
            }
        }
    }
}
