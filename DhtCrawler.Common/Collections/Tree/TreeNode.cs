namespace DhtCrawler.Common.Collections.Tree
{
    public class TreeNode<T>
    {
        public T Value { get; set; }
        public TreeNode<T> Left { get; set; }
        public TreeNode<T> Right { get; set; }
        public TreeNode(T val)
        {
            this.Value = val;
        }
    }
}