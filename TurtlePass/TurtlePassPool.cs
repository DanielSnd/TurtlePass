
    using System;

    [Serializable]
    public class TurtlePassMessagePool
    {
        public TurtlePassMessage[] _List = new TurtlePassMessage[50];
        public TurtlePassMessage[] _inUse = new TurtlePassMessage[50];
        public int ListCount = 0;
        public int inUseCount = 0;
        public TurtlePassMessage Allocate() {
            lock (_List) {
                if (ListCount != 0) {
                    TurtlePassMessage obj = _List[0];
                    RemoveFromList(obj);
                    AddToInUse(obj);
                    return obj;
                }
                else {
                    TurtlePassMessage obj = new TurtlePassMessage();
                    AddToInUse(obj);
                    return obj;
                }
            }
        }

        public static int AddToArray(TurtlePassMessage add, ref TurtlePassMessage[] array, ref int arrayCount)
        {
            if (arrayCount < 0) arrayCount = 0;
            int arrayToAdd = arrayCount;
            int desiredNewLength = arrayCount + 1;
            if (array.Length < desiredNewLength)
                Array.Resize(ref array, desiredNewLength + 50);
            arrayCount = desiredNewLength;
            array[arrayToAdd] = add;
            add.poolIndex = (arrayToAdd);
            return arrayToAdd;
        }

        public static void RemoveFromArray(TurtlePassMessage remove, ref TurtlePassMessage[] array, ref int arrayCount)
        {
            if (remove.poolIndex < 0) return;
            int replaceIndex = arrayCount - 1; int index = remove.poolIndex;
            if (replaceIndex != index && replaceIndex >= 0) {
                array[index] = array[replaceIndex];
                array[index].poolIndex = (index);
                array[replaceIndex] = null;
            } else array[index] = null;
            remove.poolIndex = (-1);
            arrayCount = arrayCount - 1;
        }

        public int AddToList(TurtlePassMessage add) => AddToArray(add, ref _List, ref ListCount);
        public void RemoveFromList(TurtlePassMessage remove) => RemoveFromArray(remove, ref _List, ref ListCount);
        public int AddToInUse(TurtlePassMessage add) {
            add.isAllocated = true;
            return AddToArray(add, ref _inUse, ref inUseCount);
        }

        public void RemoveFromInUse(TurtlePassMessage remove) => RemoveFromArray(remove, ref _inUse, ref inUseCount);
        public void Release(TurtlePassMessage obj) {
            if (!obj.isAllocated) return;
            obj.isAllocated = false;
            lock (_List) {
                RemoveFromInUse(obj);
                AddToList(obj);
            }
        }
    }