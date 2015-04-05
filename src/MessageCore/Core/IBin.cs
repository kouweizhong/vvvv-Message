﻿using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using VVVV.Packs.Messaging.Serializing;

namespace VVVV.Packs.Messaging
{
    [JsonConverter(typeof(JsonBinSerializer))]
    public interface Bin : ICloneable, ISerializable, IEnumerable, IEquatable<Bin> 
    {
//        Bin(params object[] values);
        bool ConfirmChanges();
        IList BackUp
        {
            get;
        }


 
        object First { get; set; }

        Type GetInnerType();
        object[] ToArray();

        int Count { get; set; }
        
        int Add(object item);
        void AssignFrom(IEnumerable enumerable);

        object this[int slice] {get;set;}

//        public static bool operator ==(Bin a, Bin other);
//        public static bool operator !=(Bin a, Bin other);

        void Clear();
//        IEnumerator GetEnumerator();

    }

    public interface Bin<T> : Bin, IEnumerable<T>
    {
        //new T this[int slice]
        //{
        //    get;
        //    set;
        //}

//        T First { get; set; }


    }

    public class BinFactory {

    
        #region alternative factory constructor for runtime typing of the field
        public static Bin New(Type type)
        {
            Type spreadType = typeof(BinList<>).MakeGenericType(type);
            if (TypeIdentity.Instance.ContainsKey(type))
            {
                var sl = Activator.CreateInstance(spreadType);
                return (Bin)sl;
            }
            else
            {
                throw new Exception(type.ToString() + " is not a supported Type in TypeIdentity.cs");
            }
        }

        public static Bin<T> New<T>(IEnumerable<T> values)
        {
            var type = typeof(T);

            var bin = New(type) as Bin<T>;
            bin.AssignFrom(values);
            return bin;
        }

        public static Bin<T> New<T>(params T[] values) 
        {
            var type = typeof(T);

            var bin = New(type) as Bin<T>;
            bin.AssignFrom(values);
            return bin;
        }

        #endregion
    }



}
