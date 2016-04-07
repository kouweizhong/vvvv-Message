#region usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using VVVV.Packs.Time;
using Newtonsoft.Json;
using VVVV.Packs.Messaging.Serializing;


#endregion usings

namespace VVVV.Packs.Messaging {
    using Time = VVVV.Packs.Time.Time;

    public delegate void MessageChangedWithDetails(Message original, Message change);
    public delegate void MessageChanged(Message original);
	
//	[DataContract]
    [JsonConverter(typeof(JsonMessageSerializer))]
	public class Message : ICloneable //, ISerializable
    {
        #region Properties and FieldNames
        // Access to the the inner Data.
        public IEnumerable<string> Fields
        {
            get { return Data.Keys; }
        }

        private string _topic;
        private bool _isTopicChanged = false;
        
        public string Topic{
			get {
                return _topic;
            }
            set
            {
                // todo: validate topic to be namespace-syntax
                _isTopicChanged = true;
                _topic = value;
            }
		}

        public Time TimeStamp
        {
            get;
            set;
        }
		

        internal Dictionary<string, Bin> Data = new Dictionary<string, Bin>();

        public event MessageChangedWithDetails ChangedWithDetails;
        public event MessageChanged Changed;
        #endregion

        #region Constructor
        public Message()
        {
            Topic = "vvvv";
            TimeStamp = Time.CurrentTime(); // init with local timezone
        }

        public Message(string topic)
        {
            Topic = topic;
            TimeStamp = Time.CurrentTime(); // init with local timezone
        }

       
        public Message(MessageFormular formular)
            : base()
        {
            SetFormular(formular);
        }
        #endregion

        #region Formular
        public MessageFormular GetFormular()
        {
			return new MessageFormular(this, true);
		}

        public void SetFormular(MessageFormular formular)
        {
            foreach (string field in formular.FieldNames)
            {
                Data[field] = BinFactory.New( formular[field].Type ); // Type
                var count = formular[field].DefaultSize;
                count = count <= -1 ? 1 : count;
                Data[field].Count = count; 
            }
        }
        #endregion

        #region Bin Handling
        public void Init(string name, params object[] values)
        {
            AssignFrom(name, values);
        }

        public void Add(string name, params object[] values)
        {
            AddFrom(name, values);
        }

        public void AssignFrom(string name, IEnumerable en, Type type = null)
        {
            if (!FormularFieldDescriptor.IsValidFieldName(name)) throw new ParseFormularException("\"" + name + "\" is not a valid name for a Message's field. Only use alphanumerics, dots, hyphens and underscores. ");

            if (type == null)
            {
                var gen = en.GetType().GenericTypeArguments;

                // in case en is not generic, pick the first one and reflect
                if (gen == null || gen.Count() != 1)
                {
                    var obj = en.Cast<object>().DefaultIfEmpty(new object()).First();
                    type = obj.GetType();
                }
                else type = en.GetType().GenericTypeArguments[0];
            }

            type = TypeIdentity.Instance.FindBaseType(type); // break it down.

            if (type == null) throw new TypeNotSupportedException("The assignment for the Field [" + name + "] failed, type is not supported: " + this.Topic);

            if (!Data.ContainsKey(name) || type != Data[name].GetInnerType())
            {
                Data.Remove(name);
                Data.Add(name, BinFactory.New(type));
            }
            else
            {
                Data[name].Clear();
            }

            foreach (object o in en)
            {
                Data[name].Add(o); // implicit conversion
            }
        }

        public void AddFrom(string name, IEnumerable en)
        {
            if (!Data.ContainsKey(name))
            {
                AssignFrom(name, en);
            }
            else
            {
                foreach (object o in en)
                {
                    Data[name].Add(o); // implicit conversion
                }
            }
        }

        public void Remove(string fieldname)
        {
            Data.Remove(fieldname);
        }

        public bool Rename(string fieldname, string newName, bool overwrite=false)
        {
            if (!overwrite && Data.ContainsKey(newName)) return false;
            else
            {
                var bin = Data[fieldname];
                Data[newName] = bin;
                bin.IsDirty = true;

                Remove(fieldname);
            }
            return true;
        }


        #endregion

        #region Bin Access

        public Bin this[string name]
		{
			get { 
				if (Data.ContainsKey(name)) return Data[name];
					else return null;				
			} 
			set {
                if (!FormularFieldDescriptor.IsValidFieldName(name)) throw new ParseFormularException("\"" + name + "\" is not a valid name for a Message's field. Only use alphanumerics, dots, hyphens and underscores. ");
                Data[name] = (Bin) value; 
            }
		}

        #endregion

        #region Matching

        //      use simple wildcard pattern: use * for any amount of characters (including 0) or ? for exactly one character.
        public static Regex CreateWildCardRegex(string wildcardPattern)
        {
            var regexPattern = "^" + Regex.Escape(wildcardPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            return regex;
        }

        public bool TopicMatch(Regex regex)
        {
            return regex.IsMatch(Topic ?? "");
        }

        public void InjectWith(Message message, bool allowNewFields, bool deepInspection = false)
        {
            if (this.Equals(message)) return;

            this.Topic = message.Topic;

            var keys = message.Fields;
            if (!allowNewFields) keys = keys.Intersect(this.Fields);

            foreach (var name in keys)
            {
                if (!this.Data.ContainsKey(name))
                {
                    this.AssignFrom(name, message.Data[name]);
                }
                else
                {
                    var newType = message.Data[name].GetInnerType();
                    var oldType = this.Data[name].GetInnerType();

                    if (newType.IsCastableTo(oldType))
                    {
                        if (!deepInspection || !Data[name].Equals(message.Data[name]))  // inject only if it brings new data
                            Data[name].AssignFrom(message.Data[name]); // autocast.
                    }
                    else
                    {
                        throw new BinTypeMismatchException("Cannot replace Bin<" + TypeIdentity.Instance.FindAlias(oldType) +
                                            "> with Bin<" + TypeIdentity.Instance.FindAlias(newType) + "> implicitly.");
                    }
                }
            }
        }
        #endregion

        #region Change Management

        public bool IsChanged
        {
            // check all bins, if at least one has changed since the last Sync.
            get {
                return _isTopicChanged || Data.Values.Any(bin => bin.IsDirty);
            }
            
            // reset all bins, if IsChanged is forced to false.
            internal set {
                if (!value)
                {
                    foreach (var bin in Data.Values)
                        if (bin.IsDirty) bin.IsDirty = false;
                    _isTopicChanged = value;
                }
            }
        }


        public bool Sync()
        {
            var changedFields = new List<string>();
            foreach (var field in Fields)
            {
                if (Data[field].IsDirty)
                {
                    changedFields.Add(field);
                    Data[field].IsDirty = false;
                }
            }
            
            if (changedFields.Count > 0 || _isTopicChanged)
            {
                TimeStamp = Time.CurrentTime(); // timestamp always shows last Synced Change.

                if (ChangedWithDetails != null) // for all subscribers with detailled interest
                {
                    var changedMessage = new Message(this.Topic);
                    changedMessage.TimeStamp = TimeStamp;

                    foreach (var field in changedFields)
                        changedMessage.Data[field] = Data[field].Clone() as Bin;  // deep clone

                    ChangedWithDetails(this, changedMessage); // inform all subscribers of this particular Message
                }

                if (Changed != null) // for all subscribers with only superficial interest.
                {
                    Changed(this);
                }

                _isTopicChanged = false;
                return true;
            }
            else return false;
        }



        #endregion

        #region Utils
        public bool IsEmpty
        {
            get
            {
                return Fields.Count() == 0;
            }
        }

        public object Clone() {
            // might be faster when utilizing binary serialisation.

			Message m = new Message();
			m.Topic = Topic;
			m.TimeStamp = TimeStamp;
			
			foreach (string name in Data.Keys) {
				var list = Data[name];
                var type = list.GetInnerType();
                var newList = BinFactory.New(type, list.Count);

				// deep cloning of all fields, but messages: nested messages are merely a reference by design.
                bool isPrimitiveType = type.IsPrimitive || type.IsValueType || (type == typeof(string));

                if (isPrimitiveType || type == typeof(Message))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        newList.Add(list[i]); // if primitive -> auto copy. if Message -> reference only
                    }
                }
                else
                {
                    if (typeof(ICloneable).IsAssignableFrom(type)) {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var clone = ((ICloneable)list[i]).Clone();
                            newList.Add(clone);
                        }
                    } else throw new SystemException(type.FullName + " cannot be cloned nor copied, while cloning the message "+this.Topic);
                }
                
                m[name] = newList; // add list to new Message
            }
			return m;
		}
		
        public override string ToString() {
			var sb = new StringBuilder();
			
			sb.Append("Message "+Topic+" ("+TimeStamp.LocalTime+" ["+TimeStamp.TimeZone.Id+"])\n");
			foreach (string name in Data.Keys.OrderBy(x => x)) {
				
				sb.Append(" "+name + " \t: ");
				foreach(object o in Data[name]) {
					sb.Append(o.ToString()+" ");
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}
        #endregion 

    }
}