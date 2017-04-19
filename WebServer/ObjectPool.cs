/*
* The ObjectPool generic type used for contexts and buffers.
* If not data in the pool the generic routine is call  
**/

using System;
using System.Collections.Generic;

namespace WebServer
{
    public class ObjectPool<T> where T : class
    {

        private readonly CreateHandler<T> createmethod;
        private readonly Queue<T> items = new Queue<T>();

        public ObjectPool()
        {

        }
        public ObjectPool(CreateHandler<T> createHandler)
        {
            createmethod = createHandler;
        }
        public T Dequeue()
        {
            lock (items)
            {
                if (items.Count > 0)
                    return items.Dequeue();
            }

            return createmethod();
        }
        public void Enqueue(T value)
        {
            lock (items)
                items.Enqueue(value);
        }
        public long count()
        {
            lock (items)
            {
                return (items.Count);
            }
        }
    }

    public delegate T CreateHandler<T>() where T : class;
}