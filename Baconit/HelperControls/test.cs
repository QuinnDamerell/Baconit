using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.HelperControls
{
    class test<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void InsertRange(int index, List<T> items)
        {
            List<T> item = (List<T>)Items;
            item.InsertRange(index, items);

            if(CollectionChanged != null)
            {
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, index);
                CollectionChanged(this, args);
            }
            
        }

        public new void Add(T item)
        {
            Items.Add(item);
            if (CollectionChanged != null)
            {
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, Items.Count - 1);
                CollectionChanged(this, args);
            }
        }
    }
}
