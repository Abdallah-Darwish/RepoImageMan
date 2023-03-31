using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dapper;

namespace RepoImageMan
{
    public class Commodity : IDisposable, IObservable<string>
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
        private readonly ISubject<string> _notificationsSubject = new Subject<string>();

        public IDisposable Subscribe(IObserver<string> observer) => _notificationsSubject.Subscribe(observer);

        //Kept as a seperate method in case I want to support INotifyPropertyChanged in the future.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnPropertyChanged([CallerMemberName] string propName = null) => _notificationsSubject.OnNext(propName);

        ///<summary>
        /// Order or position of this <see cref="Commodity"/> in the <see cref="Package"/>.
        /// Its unique and has no gaps.
        /// The first <see cref="Commodity"/> position in <see cref="CommodityPackage"/> is 0.
        /// </summary>
        /// <remarks>Gave position to <see cref="Commodity"/> instead of <see cref="CImage"/> because of the commodities with no images.</remarks>
        public int Position { get; private set; }

        /*
        Only the affected commodity should move
        Only the affected Image should move

        for the in betweens we can just update the property without collections
        - One way to do so is by signaling set position from inside the image
        */

        /// <summary>
        /// ONLY ONE OPERATION ACCROSS <see cref="CommodityPackage"/> AT A TIME.
        /// Changes the position of this <see cref="Commodity"/> and adjusts the positions of other <see cref="Commodity"/>s.
        /// This operation will be permanently saved to db and won't be undone after calling <see cref="Reload"/>.
        /// If the <paramref name="newPosition"/> is less than <see cref="Position"/> then all of the commodities in between will be shifted down,
        /// else all of them will be shifted up.
        /// </summary>
        /// <param name="newPosition">
        /// The new position of this <see cref="Commodity"/>.
        /// Less than 1(ex: <see cref="int.MinValue"/>) to set it as the first commodity.
        /// Any value higher than maximum position(ex: <see cref="int.MaxValue"/>) in db to set it as last.
        /// </param>
        /// <remarks>
        /// One operation across pkg because of the UNIQUE constraint on column Position.
        /// The first position in package is 0.
        /// </remarks>
        public async ValueTask SetPosition(int newPosition)
        {
            await Package._imageRepositinningLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var con = Package.GetConnection();
                con.Open();

                if (newPosition < 1) { newPosition = 0; }

                int maxPosition = Package.Commodities.Any() ? Package.Commodities.Max(c => c.Position) : 0;
                if (newPosition > maxPosition) { newPosition = maxPosition; }

                if (newPosition == Position) { return; }

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
            finally
            {
                Package._imageRepositinningLock.Release();
            }
        }

        /// <summary>
        /// Only will changes CURRENT INSTANCE position and raise related events.
        /// </summary>
        internal async Task ChangePosition(int newPosition, SQLiteConnection con)
        {
            await con.ExecuteAsync("UPDATE Commodity SET position = @newPosition WHERE id = @Id", new { Id, newPosition }).ConfigureAwait(false);
            Position = newPosition;
            OnPropertyChanged(nameof(Position));
        }

        protected Commodity(int id, CommodityPackage package)
        {
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
        public int Id { get; private set; }

        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { throw new ArgumentNullException(nameof(value)); }

                if (value == _name) { return; }

                _name = value;
                OnPropertyChanged();
            }
        }

        private decimal _wholePrice;

        public decimal WholePrice
        {
            get => _wholePrice;
            set
            {
                if (value < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(WholePrice)} can't < 0.");
                }

                if (value == _wholePrice) { return; }

                _wholePrice = value;
                OnPropertyChanged();
            }
        }

        private decimal _partialPrice;

        public decimal PartialPrice
        {
            get => _partialPrice;
            set
            {
                if (value < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(PartialPrice)} can't < 0.");
                }

                if (value == _partialPrice) { return; }

                _partialPrice = value;
                OnPropertyChanged();
            }
        }

        private decimal _cashPrice;

        public decimal CashPrice
        {
            get => _cashPrice;
            set
            {
                if (value < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(CashPrice)} can't < 0.");
                }

                if (value == _cashPrice) { return; }

                _cashPrice = value;
                OnPropertyChanged();
            }
        }

        private decimal _cost;

        public decimal Cost
        {
            get => _cost;
            set
            {
                if (value < 0m) { throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Cost)} can't < 0."); }

                if (value == _cost) { return; }

                _cost = value;
                OnPropertyChanged();
            }
        }
        private bool _isExported;
        public bool IsExported
        {
            get => _isExported;
            set
            {
                if (value == _isExported) { return; }
                _isExported = value;
                OnPropertyChanged();
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
            await con
                .ExecuteAsync("UPDATE Commodity SET name = @Name, wholePrice = @WholePrice, partialPrice = @PartialPrice, cashPrice = @CashPrice, cost = @Cost, isExported = @IsExported WHERE id = @Id", this)
                .ConfigureAwait(false);
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
            CashPrice = (decimal)(double)dbFields.CashPrice;
            Cost = (decimal)(double)dbFields.Cost;
            Position = (int)dbFields.Position;
            IsExported = (bool)dbFields.IsExported;
        }

        public override string ToString() => $"{Id}: {Name}";

        internal async Task Tidy(int newId, SQLiteConnection con)
        {
            await con.ExecuteAsync("UPDATE Commodity SET id = @newId WHERE id = @Id", new { Id, newId }).ConfigureAwait(false);
            Id = newId;
        }
        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Deleting = null;
                _notificationsSubject.OnCompleted();
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