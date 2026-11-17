using Microsoft.EntityFrameworkCore;
using Npgsql;
using WaveMP.Data.Configurations;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class UserRepository(WaveMpDbContext dbContext) : IUserRepository
{
    public Task<Page<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = dbContext.Users.AsNoTracking();
        if (query.Role is { } role)
        {
            users = users.Where(user => user.Role == role);
        }

        IOrderedQueryable<User> sorted = query.Sort == UserSortField.CreatedAt
            ? users.OrderBy(user => user.CreatedAt, query.Direction)
            : users.OrderBy(user => user.NormalizedName, query.Direction);

        return sorted
            .ThenBy(user => user.Id, query.Direction)
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public Task<Page<User>> ListProfilesAsync(int offset, int limit, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AsNoTracking()
            .OrderBy(user => user.IsBuiltIn)
            .ThenBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .ToPageAsync(offset, limit, cancellationToken);
    }

    public Task<User?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .Include(user => user.Settings)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(
            user => user.NormalizedName == normalizedName && user.Id != exceptId,
            cancellationToken);
    }

    public Task<bool> BuiltInAdminExistsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.IsBuiltIn, cancellationToken);
    }

    public Task<User?> FindBuiltInAdminAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.IsBuiltIn, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListColorsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Users.Select(user => user.Color).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);
        await SaveChangesAsync(user, cancellationToken);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(user, cancellationToken);
    }

    public async Task<PinAttempt?> BeginPinAttemptAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        DateTimeOffset lockoutEnd,
        CancellationToken cancellationToken = default)
    {
        // One statement: PostgreSQL locks the row and checks the condition again after a parallel attempt, so every
        // attempt sees the count of the one before. All expressions read the values before the update.
        int counted = await dbContext.Users
            .Where(user => user.Id == userId && (user.PinLockoutEnd == null || user.PinLockoutEnd <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        user => user.PinFailedAttempts,
                        user => (user.PinLockoutEnd == null ? user.PinFailedAttempts : 0) + 1)
                    .SetProperty(
                        user => user.PinLockoutEnd,
                        user => (user.PinLockoutEnd == null ? user.PinFailedAttempts : 0) + 1 >= maxFailedAttempts
                            ? (DateTimeOffset?)lockoutEnd
                            : null),
                cancellationToken);

        var state = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.PinFailedAttempts, user.PinLockoutEnd })
            .FirstOrDefaultAsync(cancellationToken);
        return state is null ? null : new PinAttempt(counted > 0, state.PinFailedAttempts, state.PinLockoutEnd);
    }

    public Task ResetPinAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.PinFailedAttempts, 0)
                    .SetProperty(user => user.PinLockoutEnd, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task<bool> SetPinHashAsync(
        Guid userId,
        string? pinHash,
        CancellationToken cancellationToken = default)
    {
        int updated = await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.PinHash, pinHash)
                    .SetProperty(user => user.PinFailedAttempts, 0)
                    .SetProperty(user => user.PinLockoutEnd, (DateTimeOffset?)null),
                cancellationToken);
        return updated > 0;
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Remove(user);
        await SaveChangesAsync(user, cancellationToken);
    }

    private async Task SaveChangesAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Users have no concurrency token: an update or deletion that changed no row means the user (with its
            // settings) was deleted since it was loaded, e.g. while the request ran.
            dbContext.ChangeTracker.Clear();
            throw new UserNotFoundException(user.Id, exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: UserConfiguration.SettingsUserForeignKey,
        })
        {
            // The first settings of a user that was deleted since it was loaded.
            dbContext.ChangeTracker.Clear();
            throw new UserNotFoundException(user.Id, exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserConfiguration.NormalizedNameIndex,
        })
        {
            // Nothing of the failed change stays tracked for later saves of this scope.
            dbContext.ChangeTracker.Clear();
            throw new UserNameConflictException(exception);
        }
    }
}
