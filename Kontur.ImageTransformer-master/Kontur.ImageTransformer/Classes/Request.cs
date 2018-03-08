using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Classes
{
    class Request
    {
        private DateTime _created;
        private Task _task;
        
        public DateTime Created
        {
            get { return _created; }
        }
        public Task Task
        {
            get { return _task; }
        }

        public Request(Task task)
        {
            _task = task;
            _created = DateTime.Now;
        }

        public override bool Equals(object obj)
        {
            var item = obj as Task;

            if (item == null)
            {
                return false;
            }

            return _task.Equals(item);
        }

        public override int GetHashCode()
        {
            return _task.GetHashCode();
        }
    }
}
