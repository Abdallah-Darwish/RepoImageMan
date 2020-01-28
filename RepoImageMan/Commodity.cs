using Dapper;
using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace RepoImageMan
{
    //TODO: implement IEquatable
    public class Commodity : IDisposable, INotifyPropertyChanged, INotifySpecificPropertyChanged
    {
        public delegate void CommodityDeletedEventHandler(Commodity sender);
        /// <summary>
        /// Occurs BEFORE this <see cref="Commodity"/> is about to be removed from the <see cref="Package"/>.
        /// </summary>
        public event CommodityDeletedEventHandler? Deleting;
        /// <summary>
        /// Deletes the image from <see cref="Package"/>.
        /// Will raise <see cref="CommodityPackage.CommodityRemoved"/> and <see cref="Commodity.Deleting"/>.
        /// </summary>
        public virtual async Task Delete()
        {
            Deleting?.Invoke(this);
            await Package.RemoveCommodity(this).ConfigureAwait(false);
            await using var con = Package.GetConnection();
            await con.ExecuteAsync(@"DELETE FROM Commodity WHERE id = @id;", new { Id }).ConfigureAwait(false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected readonly NotificationManager _propertyNotificationManager;
        public INotificationManager PropertyNotificationManager => _propertyNotificationManager;
        protected void OnPropertyChanged(string propName)
        {
            _propertyNotificationManager.OnPropertyChanged(propName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        ///<summary>
        /// Order or position of this <see cref="Commodity"/> in the <see cref="Package"/>.
        /// Its unique and has no gaps.
        /// </summary>
        /// <remarks>Gave position to <see cref="Commodity"/> instead of <see cref="CImage"/> because of the commodities with no images.</remarks>
        public int Position { get; private set; }


        /// <summary>
        /// ONLY ONE OPERATION ACCROSS <see cref="CommodityPackage"/> AT A TIME.
        /// Changes the position of this <see cref="Commodity"/> and adjusts the positions of other <see cref="Commodity"/>s.
        /// This operation will be permanently saved to db and won't be undone after calling <see cref="Reload"/>.
        /// </summary>
        /// <param name="newPosition">
        /// The new position of this <see cref="Commodity"/>.
        /// Less than 2(ex: <see cref="int.MinValue"/>) to set it as the firts commodity.
        /// Any value higher than maximum position(ex: <see cref="int.MaxValue"/>) in db to set it as last.
        /// </param>
        /// <remarks>
        /// One operation across pkg because of the UNIQUE constraint on column Position.
        /// The first position in package is 0.
        /// </remarks>
        public async ValueTask SetPosition(int newPosition)
        {
            if (newPosition == Position) { return; }

            await using var con = Package.GetConnection();
            con.Open();

            if (newPosition < 1) { newPosition = 0; }
            int maxPosition = Package.Commodities.Any() == false ? 0 : Package.Commodities.Max(c => c.Position);
            if (newPosition > maxPosition) { newPosition = maxPosition; }


            await con.ExecuteAsync("UPDATE Commodity SET position = NULL WHERE id = @Id", new { Id }).ConfigureAwait(false);
            if (newPosition < Position)
            {
                var comsToMove = Package.Commodities
                    .Where(c => c.Position >= newPosition && c.Position <= Position && c.Id != Id)
                    .OrderByDescending(c => c.Position)
                    .ToArray();
                foreach (var com in comsToMove)
                {
                    await com.ChangePosition(com.Position + 1, con).ConfigureAwait(false);
                }
            }
            else
            {
                var comsToMove = Package.Commodities
                    .Where(c => c.Position >= Position && c.Position <= newPosition && c.Id != Id)
                    .OrderBy(c => c.Position)
                    .ToArray();
                foreach (var com in comsToMove)
                {
                    await com.ChangePosition(com.Position - 1, con).ConfigureAwait(false);
                }
            }
            await ChangePosition(newPosition, con).ConfigureAwait(false);
        }
        /// <summary>
        /// Only will change CURRENT INSTANCE position and raise related events.
        /// </summary>
        private async Task ChangePosition(int newPosition, SQLiteConnection con)
        {
            await con.ExecuteAsync("UPDATE Commodity SET position = @newPosition WHERE id = @Id", new { Id, newPosition })
                .ConfigureAwait(false);
            Position = newPosition;
            OnPropertyChanged(nameof(Position));
        }
        protected Commodity(int id, CommodityPackage package)
        {
            _propertyNotificationManager = new NotificationManager(this);
            Package = package;
            Id = id;
        }
        /// <summary>
        /// Loads a commodity from the package correctly.
        /// </summary>
        /// <param name="id">Id of the commodity to load.</param>
        /// <param name="package">The package that contains the commodity to load.</param>
        /// <remarks>Don't use for loading an <see cref="ImageCommodity"/>.</remarks>
        internal static async Task<Commodity> Load(int id, CommodityPackage package)
        {
            var res = new Commodity(id, package);
            await res.Reload().ConfigureAwait(false);
            return res;
        }
        /// <summary>
        /// Id of this <see cref="Commodity"/> in <see cref="Package"/>.
        /// </summary>
        public int Id { get; }
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { throw new ArgumentNullException(nameof(value)); }
                if (value == _name) { return; }
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        private decimal _wholePrice;
        public decimal WholePrice
        {
            get => _wholePrice;
            set
            {
                if (value < 0m) { throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(WholePrice)} can't < 0."); }
                if (value == _wholePrice) { return; }
                _wholePrice = value;
                OnPropertyChanged(nameof(WholePrice));
            }
        }

        private decimal _partialPrice;
        public decimal PartialPrice
        {
            get => _partialPrice;
            set
            {
                if (value < 0m) { throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(PartialPrice)} can't < 0."); }
                if (value == _partialPrice) { return; }
                _partialPrice = value;
                OnPropertyChanged(nameof(PartialPrice));
            }
        }
        private decimal _cost;
        public decimal Cost
        {
            get => _cost;
            set
            {
                if (value < 0m) { throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(Cost)} can't < 0."); }
                if (value == _cost) { return; }
                _cost = value;
                OnPropertyChanged(nameof(Cost));
            }
        }
        /// <summary>
        /// The <see cref="CommodityPackage"/> that this <see cref="Commodity"/> belongs to.
        /// </summary>
        public CommodityPackage Package { get; }


        /// <summary>
        /// Saves all the properties of the <see cref="Commodity"/> to the <see cref="CommodityPackage"/>.
        /// </summary>
        public virtual async Task Save()
        {
            await using var con = Package.GetConnection();
            await con.ExecuteAsync("UPDATE Commodity SET name = @Name, wholePrice = @WholePrice, partialPrice = @PartialPrice, cost = @Cost WHERE id = @Id", this).ConfigureAwait(false);
        }
        /// <summary>
        /// Re-reads all <see cref="Commodity"/> properties from the <see cref="CommodityPackage"/>.
        /// Will raise <see cref="PropertyChanged"/>.
        /// </summary>
        public virtual async Task Reload()
        {
            await using var con = Package.GetConnection();
            var dbFields = await con.QueryFirstAsync("SELECT * FROM Commodity WHERE id = @Id", new { Id }).ConfigureAwait(false);
            Name = dbFields.Name;
            WholePrice = (decimal)(double)dbFields.WholePrice;
            PartialPrice = (decimal)(double)dbFields.PartialPrice;
            Cost = (decimal)(double)dbFields.Cost;
            Position = (int)dbFields.Position;
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Deleting = null;
                PropertyChanged = null;
                _propertyNotificationManager.Dispose();
                _disposedValue = true;
            }
        }

        /// <summary>
        /// You shouldn't call this explecitly, instead call <see cref="CommodityPackage.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
