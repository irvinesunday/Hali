using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NSubstitute;
using StackExchange.Redis;

namespace Hali.Tests.Unit.Signals;

internal sealed class FakeRedisDatabase : IDatabase, IRedis, IRedisAsync, IDatabaseAsync
{
	private readonly IDatabase _inner = Substitute.For<IDatabase>(Array.Empty<object>());

	public bool StringSetCalled { get; private set; }

	public string? LastStringSetKey { get; private set; }

	public int Database => _inner.Database;

	public IConnectionMultiplexer Multiplexer => _inner.Multiplexer;

	public FakeRedisDatabase()
	{
		_inner.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
	}

	public IBatch CreateBatch(object asyncState)
	{
		return _inner.CreateBatch(asyncState);
	}

	public ITransaction CreateTransaction(object asyncState)
	{
		return _inner.CreateTransaction(asyncState);
	}

	public void KeyMigrate(RedisKey key, EndPoint toServer, int toDatabase, int timeoutMilliseconds, MigrateOptions migrateOptions, CommandFlags flags)
	{
		_inner.KeyMigrate(key, toServer, toDatabase, timeoutMilliseconds, migrateOptions, flags);
	}

	public RedisValue DebugObject(RedisKey key, CommandFlags flags)
	{
		return _inner.DebugObject(key, flags);
	}

	public bool GeoAdd(RedisKey key, double longitude, double latitude, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoAdd(key, longitude, latitude, member, flags);
	}

	public bool GeoAdd(RedisKey key, GeoEntry value, CommandFlags flags)
	{
		return _inner.GeoAdd(key, value, flags);
	}

	public long GeoAdd(RedisKey key, GeoEntry[] values, CommandFlags flags)
	{
		return _inner.GeoAdd(key, values, flags);
	}

	public bool GeoRemove(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoRemove(key, member, flags);
	}

	public double? GeoDistance(RedisKey key, RedisValue member1, RedisValue member2, GeoUnit unit, CommandFlags flags)
	{
		return _inner.GeoDistance(key, member1, member2, unit, flags);
	}

	public string[] GeoHash(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.GeoHash(key, members, flags);
	}

	public string GeoHash(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoHash(key, member, flags);
	}

	public GeoPosition?[] GeoPosition(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.GeoPosition(key, members, flags);
	}

	public GeoPosition? GeoPosition(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoPosition(key, member, flags);
	}

	public GeoRadiusResult[] GeoRadius(RedisKey key, RedisValue member, double radius, GeoUnit unit, int count, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoRadius(key, member, radius, unit, count, order, options, flags);
	}

	public GeoRadiusResult[] GeoRadius(RedisKey key, double longitude, double latitude, double radius, GeoUnit unit, int count, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoRadius(key, longitude, latitude, radius, unit, count, order, options, flags);
	}

	public GeoRadiusResult[] GeoSearch(RedisKey key, RedisValue member, GeoSearchShape shape, int count, bool demandClosest, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoSearch(key, member, shape, count, demandClosest, order, options, flags);
	}

	public GeoRadiusResult[] GeoSearch(RedisKey key, double longitude, double latitude, GeoSearchShape shape, int count, bool demandClosest, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoSearch(key, longitude, latitude, shape, count, demandClosest, order, options, flags);
	}

	public long GeoSearchAndStore(RedisKey sourceKey, RedisKey destinationKey, RedisValue member, GeoSearchShape shape, int count, bool demandClosest, Order? order, bool storeDistances, CommandFlags flags)
	{
		return _inner.GeoSearchAndStore(sourceKey, destinationKey, member, shape, count, demandClosest, order, storeDistances, flags);
	}

	public long GeoSearchAndStore(RedisKey sourceKey, RedisKey destinationKey, double longitude, double latitude, GeoSearchShape shape, int count, bool demandClosest, Order? order, bool storeDistances, CommandFlags flags)
	{
		return _inner.GeoSearchAndStore(sourceKey, destinationKey, longitude, latitude, shape, count, demandClosest, order, storeDistances, flags);
	}

	public long HashDecrement(RedisKey key, RedisValue hashField, long value, CommandFlags flags)
	{
		return _inner.HashDecrement(key, hashField, value, flags);
	}

	public double HashDecrement(RedisKey key, RedisValue hashField, double value, CommandFlags flags)
	{
		return _inner.HashDecrement(key, hashField, value, flags);
	}

	public bool HashDelete(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashDelete(key, hashField, flags);
	}

	public long HashDelete(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashDelete(key, hashFields, flags);
	}

	public bool HashExists(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashExists(key, hashField, flags);
	}

	public ExpireResult[] HashFieldExpire(RedisKey key, RedisValue[] hashFields, TimeSpan expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.HashFieldExpire(key, hashFields, expiry, when, flags);
	}

	public ExpireResult[] HashFieldExpire(RedisKey key, RedisValue[] hashFields, DateTime expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.HashFieldExpire(key, hashFields, expiry, when, flags);
	}

	public long[] HashFieldGetExpireDateTime(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetExpireDateTime(key, hashFields, flags);
	}

	public PersistResult[] HashFieldPersist(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldPersist(key, hashFields, flags);
	}

	public long[] HashFieldGetTimeToLive(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetTimeToLive(key, hashFields, flags);
	}

	public RedisValue HashGet(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashGet(key, hashField, flags);
	}

	public Lease<byte> HashGetLease(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashGetLease(key, hashField, flags);
	}

	public RedisValue[] HashGet(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashGet(key, hashFields, flags);
	}

	public RedisValue HashFieldGetAndDelete(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashFieldGetAndDelete(key, hashField, flags);
	}

	public Lease<byte> HashFieldGetLeaseAndDelete(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndDelete(key, hashField, flags);
	}

	public RedisValue[] HashFieldGetAndDelete(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetAndDelete(key, hashFields, flags);
	}

	public RedisValue HashFieldGetAndSetExpiry(RedisKey key, RedisValue hashField, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiry(key, hashField, expiry, persist, flags);
	}

	public RedisValue HashFieldGetAndSetExpiry(RedisKey key, RedisValue hashField, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiry(key, hashField, expiry, flags);
	}

	public Lease<byte> HashFieldGetLeaseAndSetExpiry(RedisKey key, RedisValue hashField, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndSetExpiry(key, hashField, expiry, persist, flags);
	}

	public Lease<byte> HashFieldGetLeaseAndSetExpiry(RedisKey key, RedisValue hashField, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndSetExpiry(key, hashField, expiry, flags);
	}

	public RedisValue[] HashFieldGetAndSetExpiry(RedisKey key, RedisValue[] hashFields, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiry(key, hashFields, expiry, persist, flags);
	}

	public RedisValue[] HashFieldGetAndSetExpiry(RedisKey key, RedisValue[] hashFields, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiry(key, hashFields, expiry, flags);
	}

	public RedisValue HashFieldSetAndSetExpiry(RedisKey key, RedisValue field, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiry(key, field, value, expiry, keepTtl, when, flags);
	}

	public RedisValue HashFieldSetAndSetExpiry(RedisKey key, RedisValue field, RedisValue value, DateTime expiry, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiry(key, field, value, expiry, when, flags);
	}

	public RedisValue HashFieldSetAndSetExpiry(RedisKey key, HashEntry[] hashFields, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiry(key, hashFields, expiry, keepTtl, when, flags);
	}

	public RedisValue HashFieldSetAndSetExpiry(RedisKey key, HashEntry[] hashFields, DateTime expiry, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiry(key, hashFields, expiry, when, flags);
	}

	public HashEntry[] HashGetAll(RedisKey key, CommandFlags flags)
	{
		return _inner.HashGetAll(key, flags);
	}

	public long HashIncrement(RedisKey key, RedisValue hashField, long value, CommandFlags flags)
	{
		return _inner.HashIncrement(key, hashField, value, flags);
	}

	public double HashIncrement(RedisKey key, RedisValue hashField, double value, CommandFlags flags)
	{
		return _inner.HashIncrement(key, hashField, value, flags);
	}

	public RedisValue[] HashKeys(RedisKey key, CommandFlags flags)
	{
		return _inner.HashKeys(key, flags);
	}

	public long HashLength(RedisKey key, CommandFlags flags)
	{
		return _inner.HashLength(key, flags);
	}

	public RedisValue HashRandomField(RedisKey key, CommandFlags flags)
	{
		return _inner.HashRandomField(key, flags);
	}

	public RedisValue[] HashRandomFields(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.HashRandomFields(key, count, flags);
	}

	public HashEntry[] HashRandomFieldsWithValues(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.HashRandomFieldsWithValues(key, count, flags);
	}

	public IEnumerable<HashEntry> HashScan(RedisKey key, RedisValue pattern, int pageSize, CommandFlags flags)
	{
		return _inner.HashScan(key, pattern, pageSize, flags);
	}

	public IEnumerable<HashEntry> HashScan(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.HashScan(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public IEnumerable<RedisValue> HashScanNoValues(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.HashScanNoValues(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public void HashSet(RedisKey key, HashEntry[] hashFields, CommandFlags flags)
	{
		_inner.HashSet(key, hashFields, flags);
	}

	public bool HashSet(RedisKey key, RedisValue hashField, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.HashSet(key, hashField, value, when, flags);
	}

	public long HashStringLength(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashStringLength(key, hashField, flags);
	}

	public RedisValue[] HashValues(RedisKey key, CommandFlags flags)
	{
		return _inner.HashValues(key, flags);
	}

	public bool HyperLogLogAdd(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.HyperLogLogAdd(key, value, flags);
	}

	public bool HyperLogLogAdd(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.HyperLogLogAdd(key, values, flags);
	}

	public long HyperLogLogLength(RedisKey key, CommandFlags flags)
	{
		return _inner.HyperLogLogLength(key, flags);
	}

	public long HyperLogLogLength(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.HyperLogLogLength(keys, flags);
	}

	public void HyperLogLogMerge(RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		_inner.HyperLogLogMerge(destination, first, second, flags);
	}

	public void HyperLogLogMerge(RedisKey destination, RedisKey[] sourceKeys, CommandFlags flags)
	{
		_inner.HyperLogLogMerge(destination, sourceKeys, flags);
	}

	public EndPoint IdentifyEndpoint(RedisKey key, CommandFlags flags)
	{
		return _inner.IdentifyEndpoint(key, flags);
	}

	public bool KeyCopy(RedisKey sourceKey, RedisKey destinationKey, int destinationDatabase, bool replace, CommandFlags flags)
	{
		return _inner.KeyCopy(sourceKey, destinationKey, destinationDatabase, replace, flags);
	}

	public bool KeyDelete(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyDelete(key, flags);
	}

	public long KeyDelete(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyDelete(keys, flags);
	}

	public byte[] KeyDump(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyDump(key, flags);
	}

	public string KeyEncoding(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyEncoding(key, flags);
	}

	public bool KeyExists(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyExists(key, flags);
	}

	public long KeyExists(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyExists(keys, flags);
	}

	public bool KeyExpire(RedisKey key, TimeSpan? expiry, CommandFlags flags)
	{
		return _inner.KeyExpire(key, expiry, flags);
	}

	public bool KeyExpire(RedisKey key, TimeSpan? expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.KeyExpire(key, expiry, when, flags);
	}

	public bool KeyExpire(RedisKey key, DateTime? expiry, CommandFlags flags)
	{
		return _inner.KeyExpire(key, expiry, flags);
	}

	public bool KeyExpire(RedisKey key, DateTime? expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.KeyExpire(key, expiry, when, flags);
	}

	public DateTime? KeyExpireTime(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyExpireTime(key, flags);
	}

	public long? KeyFrequency(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyFrequency(key, flags);
	}

	public TimeSpan? KeyIdleTime(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyIdleTime(key, flags);
	}

	public bool KeyMove(RedisKey key, int database, CommandFlags flags)
	{
		return _inner.KeyMove(key, database, flags);
	}

	public bool KeyPersist(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyPersist(key, flags);
	}

	public RedisKey KeyRandom(CommandFlags flags)
	{
		return _inner.KeyRandom(flags);
	}

	public long? KeyRefCount(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyRefCount(key, flags);
	}

	public bool KeyRename(RedisKey key, RedisKey newKey, When when, CommandFlags flags)
	{
		return _inner.KeyRename(key, newKey, when, flags);
	}

	public void KeyRestore(RedisKey key, byte[] value, TimeSpan? expiry, CommandFlags flags)
	{
		_inner.KeyRestore(key, value, expiry, flags);
	}

	public TimeSpan? KeyTimeToLive(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyTimeToLive(key, flags);
	}

	public bool KeyTouch(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyTouch(key, flags);
	}

	public long KeyTouch(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyTouch(keys, flags);
	}

	public RedisType KeyType(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyType(key, flags);
	}

	public RedisValue ListGetByIndex(RedisKey key, long index, CommandFlags flags)
	{
		return _inner.ListGetByIndex(key, index, flags);
	}

	public long ListInsertAfter(RedisKey key, RedisValue pivot, RedisValue value, CommandFlags flags)
	{
		return _inner.ListInsertAfter(key, pivot, value, flags);
	}

	public long ListInsertBefore(RedisKey key, RedisValue pivot, RedisValue value, CommandFlags flags)
	{
		return _inner.ListInsertBefore(key, pivot, value, flags);
	}

	public RedisValue ListLeftPop(RedisKey key, CommandFlags flags)
	{
		return _inner.ListLeftPop(key, flags);
	}

	public RedisValue[] ListLeftPop(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.ListLeftPop(key, count, flags);
	}

	public ListPopResult ListLeftPop(RedisKey[] keys, long count, CommandFlags flags)
	{
		return _inner.ListLeftPop(keys, count, flags);
	}

	public long ListPosition(RedisKey key, RedisValue element, long rank, long maxLength, CommandFlags flags)
	{
		return _inner.ListPosition(key, element, rank, maxLength, flags);
	}

	public long[] ListPositions(RedisKey key, RedisValue element, long count, long rank, long maxLength, CommandFlags flags)
	{
		return _inner.ListPositions(key, element, count, rank, maxLength, flags);
	}

	public long ListLeftPush(RedisKey key, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.ListLeftPush(key, value, when, flags);
	}

	public long ListLeftPush(RedisKey key, RedisValue[] values, When when, CommandFlags flags)
	{
		return _inner.ListLeftPush(key, values, when, flags);
	}

	public long ListLeftPush(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ListLeftPush(key, values, flags);
	}

	public long ListLength(RedisKey key, CommandFlags flags)
	{
		return _inner.ListLength(key, flags);
	}

	public RedisValue ListMove(RedisKey sourceKey, RedisKey destinationKey, ListSide sourceSide, ListSide destinationSide, CommandFlags flags)
	{
		return _inner.ListMove(sourceKey, destinationKey, sourceSide, destinationSide, flags);
	}

	public RedisValue[] ListRange(RedisKey key, long start, long stop, CommandFlags flags)
	{
		return _inner.ListRange(key, start, stop, flags);
	}

	public long ListRemove(RedisKey key, RedisValue value, long count, CommandFlags flags)
	{
		return _inner.ListRemove(key, value, count, flags);
	}

	public RedisValue ListRightPop(RedisKey key, CommandFlags flags)
	{
		return _inner.ListRightPop(key, flags);
	}

	public RedisValue[] ListRightPop(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.ListRightPop(key, count, flags);
	}

	public ListPopResult ListRightPop(RedisKey[] keys, long count, CommandFlags flags)
	{
		return _inner.ListRightPop(keys, count, flags);
	}

	public RedisValue ListRightPopLeftPush(RedisKey source, RedisKey destination, CommandFlags flags)
	{
		return _inner.ListRightPopLeftPush(source, destination, flags);
	}

	public long ListRightPush(RedisKey key, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.ListRightPush(key, value, when, flags);
	}

	public long ListRightPush(RedisKey key, RedisValue[] values, When when, CommandFlags flags)
	{
		return _inner.ListRightPush(key, values, when, flags);
	}

	public long ListRightPush(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ListRightPush(key, values, flags);
	}

	public void ListSetByIndex(RedisKey key, long index, RedisValue value, CommandFlags flags)
	{
		_inner.ListSetByIndex(key, index, value, flags);
	}

	public void ListTrim(RedisKey key, long start, long stop, CommandFlags flags)
	{
		_inner.ListTrim(key, start, stop, flags);
	}

	public bool LockExtend(RedisKey key, RedisValue value, TimeSpan expiry, CommandFlags flags)
	{
		return _inner.LockExtend(key, value, expiry, flags);
	}

	public RedisValue LockQuery(RedisKey key, CommandFlags flags)
	{
		return _inner.LockQuery(key, flags);
	}

	public bool LockRelease(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.LockRelease(key, value, flags);
	}

	public bool LockTake(RedisKey key, RedisValue value, TimeSpan expiry, CommandFlags flags)
	{
		return _inner.LockTake(key, value, expiry, flags);
	}

	public long Publish(RedisChannel channel, RedisValue message, CommandFlags flags)
	{
		return _inner.Publish(channel, message, flags);
	}

	public RedisResult Execute(string command, object[] args)
	{
		return _inner.Execute(command, args);
	}

	public RedisResult Execute(string command, ICollection<object> args, CommandFlags flags)
	{
		return _inner.Execute(command, args, flags);
	}

	public RedisResult ScriptEvaluate(string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluate(script, keys, values, flags);
	}

	public RedisResult ScriptEvaluate(byte[] hash, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluate(hash, keys, values, flags);
	}

	public RedisResult ScriptEvaluate(LuaScript script, object parameters, CommandFlags flags)
	{
		return _inner.ScriptEvaluate(script, parameters, flags);
	}

	public RedisResult ScriptEvaluate(LoadedLuaScript script, object parameters, CommandFlags flags)
	{
		return _inner.ScriptEvaluate(script, parameters, flags);
	}

	public RedisResult ScriptEvaluateReadOnly(string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateReadOnly(script, keys, values, flags);
	}

	public RedisResult ScriptEvaluateReadOnly(byte[] hash, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateReadOnly(hash, keys, values, flags);
	}

	public bool SetAdd(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetAdd(key, value, flags);
	}

	public long SetAdd(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetAdd(key, values, flags);
	}

	public RedisValue[] SetCombine(SetOperation operation, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.SetCombine(operation, first, second, flags);
	}

	public RedisValue[] SetCombine(SetOperation operation, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.SetCombine(operation, keys, flags);
	}

	public long SetCombineAndStore(SetOperation operation, RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.SetCombineAndStore(operation, destination, first, second, flags);
	}

	public long SetCombineAndStore(SetOperation operation, RedisKey destination, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.SetCombineAndStore(operation, destination, keys, flags);
	}

	public bool SetContains(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetContains(key, value, flags);
	}

	public bool[] SetContains(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetContains(key, values, flags);
	}

	public long SetIntersectionLength(RedisKey[] keys, long limit, CommandFlags flags)
	{
		return _inner.SetIntersectionLength(keys, limit, flags);
	}

	public long SetLength(RedisKey key, CommandFlags flags)
	{
		return _inner.SetLength(key, flags);
	}

	public RedisValue[] SetMembers(RedisKey key, CommandFlags flags)
	{
		return _inner.SetMembers(key, flags);
	}

	public bool SetMove(RedisKey source, RedisKey destination, RedisValue value, CommandFlags flags)
	{
		return _inner.SetMove(source, destination, value, flags);
	}

	public RedisValue SetPop(RedisKey key, CommandFlags flags)
	{
		return _inner.SetPop(key, flags);
	}

	public RedisValue[] SetPop(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SetPop(key, count, flags);
	}

	public RedisValue SetRandomMember(RedisKey key, CommandFlags flags)
	{
		return _inner.SetRandomMember(key, flags);
	}

	public RedisValue[] SetRandomMembers(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SetRandomMembers(key, count, flags);
	}

	public bool SetRemove(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetRemove(key, value, flags);
	}

	public long SetRemove(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetRemove(key, values, flags);
	}

	public IEnumerable<RedisValue> SetScan(RedisKey key, RedisValue pattern, int pageSize, CommandFlags flags)
	{
		return _inner.SetScan(key, pattern, pageSize, flags);
	}

	public IEnumerable<RedisValue> SetScan(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.SetScan(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public RedisValue[] Sort(RedisKey key, long skip, long take, Order order, SortType sortType, RedisValue by, RedisValue[] get, CommandFlags flags)
	{
		return _inner.Sort(key, skip, take, order, sortType, by, get, flags);
	}

	public long SortAndStore(RedisKey destination, RedisKey key, long skip, long take, Order order, SortType sortType, RedisValue by, RedisValue[] get, CommandFlags flags)
	{
		return _inner.SortAndStore(destination, key, skip, take, order, sortType, by, get, flags);
	}

	public bool SortedSetAdd(RedisKey key, RedisValue member, double score, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, member, score, flags);
	}

	public bool SortedSetAdd(RedisKey key, RedisValue member, double score, When when, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, member, score, when, flags);
	}

	public bool SortedSetAdd(RedisKey key, RedisValue member, double score, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, member, score, when, flags);
	}

	public long SortedSetAdd(RedisKey key, SortedSetEntry[] values, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, values, flags);
	}

	public long SortedSetAdd(RedisKey key, SortedSetEntry[] values, When when, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, values, when, flags);
	}

	public long SortedSetAdd(RedisKey key, SortedSetEntry[] values, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetAdd(key, values, when, flags);
	}

	public RedisValue[] SortedSetCombine(SetOperation operation, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombine(operation, keys, weights, aggregate, flags);
	}

	public SortedSetEntry[] SortedSetCombineWithScores(SetOperation operation, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineWithScores(operation, keys, weights, aggregate, flags);
	}

	public long SortedSetCombineAndStore(SetOperation operation, RedisKey destination, RedisKey first, RedisKey second, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineAndStore(operation, destination, first, second, aggregate, flags);
	}

	public long SortedSetCombineAndStore(SetOperation operation, RedisKey destination, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineAndStore(operation, destination, keys, weights, aggregate, flags);
	}

	public double SortedSetDecrement(RedisKey key, RedisValue member, double value, CommandFlags flags)
	{
		return _inner.SortedSetDecrement(key, member, value, flags);
	}

	public double SortedSetIncrement(RedisKey key, RedisValue member, double value, CommandFlags flags)
	{
		return _inner.SortedSetIncrement(key, member, value, flags);
	}

	public long SortedSetIntersectionLength(RedisKey[] keys, long limit, CommandFlags flags)
	{
		return _inner.SortedSetIntersectionLength(keys, limit, flags);
	}

	public long SortedSetLength(RedisKey key, double min, double max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetLength(key, min, max, exclude, flags);
	}

	public long SortedSetLengthByValue(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetLengthByValue(key, min, max, exclude, flags);
	}

	public RedisValue SortedSetRandomMember(RedisKey key, CommandFlags flags)
	{
		return _inner.SortedSetRandomMember(key, flags);
	}

	public RedisValue[] SortedSetRandomMembers(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SortedSetRandomMembers(key, count, flags);
	}

	public SortedSetEntry[] SortedSetRandomMembersWithScores(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SortedSetRandomMembersWithScores(key, count, flags);
	}

	public RedisValue[] SortedSetRangeByRank(RedisKey key, long start, long stop, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRangeByRank(key, start, stop, order, flags);
	}

	public long SortedSetRangeAndStore(RedisKey sourceKey, RedisKey destinationKey, RedisValue start, RedisValue stop, SortedSetOrder sortedSetOrder, Exclude exclude, Order order, long skip, long? take, CommandFlags flags)
	{
		return _inner.SortedSetRangeAndStore(sourceKey, destinationKey, start, stop, sortedSetOrder, exclude, order, skip, take, flags);
	}

	public SortedSetEntry[] SortedSetRangeByRankWithScores(RedisKey key, long start, long stop, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRangeByRankWithScores(key, start, stop, order, flags);
	}

	public RedisValue[] SortedSetRangeByScore(RedisKey key, double start, double stop, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByScore(key, start, stop, exclude, order, skip, take, flags);
	}

	public SortedSetEntry[] SortedSetRangeByScoreWithScores(RedisKey key, double start, double stop, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByScoreWithScores(key, start, stop, exclude, order, skip, take, flags);
	}

	public RedisValue[] SortedSetRangeByValue(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByValue(key, min, max, exclude, skip, take, flags);
	}

	public RedisValue[] SortedSetRangeByValue(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByValue(key, min, max, exclude, order, skip, take, flags);
	}

	public long? SortedSetRank(RedisKey key, RedisValue member, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRank(key, member, order, flags);
	}

	public bool SortedSetRemove(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.SortedSetRemove(key, member, flags);
	}

	public long SortedSetRemove(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.SortedSetRemove(key, members, flags);
	}

	public long SortedSetRemoveRangeByRank(RedisKey key, long start, long stop, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByRank(key, start, stop, flags);
	}

	public long SortedSetRemoveRangeByScore(RedisKey key, double start, double stop, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByScore(key, start, stop, exclude, flags);
	}

	public long SortedSetRemoveRangeByValue(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByValue(key, min, max, exclude, flags);
	}

	public IEnumerable<SortedSetEntry> SortedSetScan(RedisKey key, RedisValue pattern, int pageSize, CommandFlags flags)
	{
		return _inner.SortedSetScan(key, pattern, pageSize, flags);
	}

	public IEnumerable<SortedSetEntry> SortedSetScan(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.SortedSetScan(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public double? SortedSetScore(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.SortedSetScore(key, member, flags);
	}

	public double?[] SortedSetScores(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.SortedSetScores(key, members, flags);
	}

	public SortedSetEntry? SortedSetPop(RedisKey key, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPop(key, order, flags);
	}

	public SortedSetEntry[] SortedSetPop(RedisKey key, long count, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPop(key, count, order, flags);
	}

	public SortedSetPopResult SortedSetPop(RedisKey[] keys, long count, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPop(keys, count, order, flags);
	}

	public bool SortedSetUpdate(RedisKey key, RedisValue member, double score, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetUpdate(key, member, score, when, flags);
	}

	public long SortedSetUpdate(RedisKey key, SortedSetEntry[] values, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetUpdate(key, values, when, flags);
	}

	public long StreamAcknowledge(RedisKey key, RedisValue groupName, RedisValue messageId, CommandFlags flags)
	{
		return _inner.StreamAcknowledge(key, groupName, messageId, flags);
	}

	public long StreamAcknowledge(RedisKey key, RedisValue groupName, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamAcknowledge(key, groupName, messageIds, flags);
	}

	public StreamTrimResult StreamAcknowledgeAndDelete(RedisKey key, RedisValue groupName, StreamTrimMode mode, RedisValue messageId, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAndDelete(key, groupName, mode, messageId, flags);
	}

	public StreamTrimResult[] StreamAcknowledgeAndDelete(RedisKey key, RedisValue groupName, StreamTrimMode mode, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAndDelete(key, groupName, mode, messageIds, flags);
	}

	public RedisValue StreamAdd(RedisKey key, RedisValue streamField, RedisValue streamValue, RedisValue? messageId, int? maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamField, streamValue, messageId, maxLength, useApproximateMaxLength, flags);
	}

	public RedisValue StreamAdd(RedisKey key, NameValueEntry[] streamPairs, RedisValue? messageId, int? maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamPairs, messageId, maxLength, useApproximateMaxLength, flags);
	}

	public RedisValue StreamAdd(RedisKey key, RedisValue streamField, RedisValue streamValue, RedisValue? messageId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamField, streamValue, messageId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public RedisValue StreamAdd(RedisKey key, RedisValue streamField, RedisValue streamValue, StreamIdempotentId idempotentId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamField, streamValue, idempotentId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public RedisValue StreamAdd(RedisKey key, NameValueEntry[] streamPairs, RedisValue? messageId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamPairs, messageId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public RedisValue StreamAdd(RedisKey key, NameValueEntry[] streamPairs, StreamIdempotentId idempotentId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAdd(key, streamPairs, idempotentId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public void StreamConfigure(RedisKey key, StreamConfiguration configuration, CommandFlags flags)
	{
		_inner.StreamConfigure(key, configuration, flags);
	}

	public StreamAutoClaimResult StreamAutoClaim(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue startAtId, int? count, CommandFlags flags)
	{
		return _inner.StreamAutoClaim(key, consumerGroup, claimingConsumer, minIdleTimeInMs, startAtId, count, flags);
	}

	public StreamAutoClaimIdsOnlyResult StreamAutoClaimIdsOnly(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue startAtId, int? count, CommandFlags flags)
	{
		return _inner.StreamAutoClaimIdsOnly(key, consumerGroup, claimingConsumer, minIdleTimeInMs, startAtId, count, flags);
	}

	public StreamEntry[] StreamClaim(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamClaim(key, consumerGroup, claimingConsumer, minIdleTimeInMs, messageIds, flags);
	}

	public RedisValue[] StreamClaimIdsOnly(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamClaimIdsOnly(key, consumerGroup, claimingConsumer, minIdleTimeInMs, messageIds, flags);
	}

	public bool StreamConsumerGroupSetPosition(RedisKey key, RedisValue groupName, RedisValue position, CommandFlags flags)
	{
		return _inner.StreamConsumerGroupSetPosition(key, groupName, position, flags);
	}

	public StreamConsumerInfo[] StreamConsumerInfo(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamConsumerInfo(key, groupName, flags);
	}

	public bool StreamCreateConsumerGroup(RedisKey key, RedisValue groupName, RedisValue? position, CommandFlags flags)
	{
		return _inner.StreamCreateConsumerGroup(key, groupName, position, flags);
	}

	public bool StreamCreateConsumerGroup(RedisKey key, RedisValue groupName, RedisValue? position, bool createStream, CommandFlags flags)
	{
		return _inner.StreamCreateConsumerGroup(key, groupName, position, createStream, flags);
	}

	public long StreamDelete(RedisKey key, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamDelete(key, messageIds, flags);
	}

	public StreamTrimResult[] StreamDelete(RedisKey key, RedisValue[] messageIds, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamDelete(key, messageIds, mode, flags);
	}

	public long StreamDeleteConsumer(RedisKey key, RedisValue groupName, RedisValue consumerName, CommandFlags flags)
	{
		return _inner.StreamDeleteConsumer(key, groupName, consumerName, flags);
	}

	public bool StreamDeleteConsumerGroup(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamDeleteConsumerGroup(key, groupName, flags);
	}

	public StreamGroupInfo[] StreamGroupInfo(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamGroupInfo(key, flags);
	}

	public StreamInfo StreamInfo(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamInfo(key, flags);
	}

	public long StreamLength(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamLength(key, flags);
	}

	public StreamPendingInfo StreamPending(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamPending(key, groupName, flags);
	}

	public StreamPendingMessageInfo[] StreamPendingMessages(RedisKey key, RedisValue groupName, int count, RedisValue consumerName, RedisValue? minId, RedisValue? maxId, CommandFlags flags)
	{
		return _inner.StreamPendingMessages(key, groupName, count, consumerName, minId, maxId, flags);
	}

	public StreamPendingMessageInfo[] StreamPendingMessages(RedisKey key, RedisValue groupName, int count, RedisValue consumerName, RedisValue? minId, RedisValue? maxId, long? minIdleTimeInMs, CommandFlags flags)
	{
		return _inner.StreamPendingMessages(key, groupName, count, consumerName, minId, maxId, minIdleTimeInMs, flags);
	}

	public StreamEntry[] StreamRange(RedisKey key, RedisValue? minId, RedisValue? maxId, int? count, Order messageOrder, CommandFlags flags)
	{
		return _inner.StreamRange(key, minId, maxId, count, messageOrder, flags);
	}

	public StreamEntry[] StreamRead(RedisKey key, RedisValue position, int? count, CommandFlags flags)
	{
		return _inner.StreamRead(key, position, count, flags);
	}

	public RedisStream[] StreamRead(StreamPosition[] streamPositions, int? countPerStream, CommandFlags flags)
	{
		return _inner.StreamRead(streamPositions, countPerStream, flags);
	}

	public StreamEntry[] StreamReadGroup(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, CommandFlags flags)
	{
		return _inner.StreamReadGroup(key, groupName, consumerName, position, count, flags);
	}

	public StreamEntry[] StreamReadGroup(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, bool noAck, CommandFlags flags)
	{
		return _inner.StreamReadGroup(key, groupName, consumerName, position, count, noAck, flags);
	}

	public StreamEntry[] StreamReadGroup(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, bool noAck, TimeSpan? claimMinIdleTime, CommandFlags flags)
	{
		return _inner.StreamReadGroup(key, groupName, consumerName, position, count, noAck, claimMinIdleTime, flags);
	}

	public RedisStream[] StreamReadGroup(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, CommandFlags flags)
	{
		return _inner.StreamReadGroup(streamPositions, groupName, consumerName, countPerStream, flags);
	}

	public RedisStream[] StreamReadGroup(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, bool noAck, CommandFlags flags)
	{
		return _inner.StreamReadGroup(streamPositions, groupName, consumerName, countPerStream, noAck, flags);
	}

	public RedisStream[] StreamReadGroup(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, bool noAck, TimeSpan? claimMinIdleTime, CommandFlags flags)
	{
		return _inner.StreamReadGroup(streamPositions, groupName, consumerName, countPerStream, noAck, claimMinIdleTime, flags);
	}

	public long StreamTrim(RedisKey key, int maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamTrim(key, maxLength, useApproximateMaxLength, flags);
	}

	public long StreamTrim(RedisKey key, long maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamTrim(key, maxLength, useApproximateMaxLength, limit, mode, flags);
	}

	public long StreamTrimByMinId(RedisKey key, RedisValue minId, bool useApproximateMaxLength, long? limit, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamTrimByMinId(key, minId, useApproximateMaxLength, limit, mode, flags);
	}

	public long StringAppend(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.StringAppend(key, value, flags);
	}

	public long StringBitCount(RedisKey key, long start, long end, CommandFlags flags)
	{
		return _inner.StringBitCount(key, start, end, flags);
	}

	public long StringBitCount(RedisKey key, long start, long end, StringIndexType indexType, CommandFlags flags)
	{
		return _inner.StringBitCount(key, start, end, indexType, flags);
	}

	public long StringBitOperation(Bitwise operation, RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringBitOperation(operation, destination, first, second, flags);
	}

	public long StringBitOperation(Bitwise operation, RedisKey destination, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.StringBitOperation(operation, destination, keys, flags);
	}

	public long StringBitPosition(RedisKey key, bool bit, long start, long end, CommandFlags flags)
	{
		return _inner.StringBitPosition(key, bit, start, end, flags);
	}

	public long StringBitPosition(RedisKey key, bool bit, long start, long end, StringIndexType indexType, CommandFlags flags)
	{
		return _inner.StringBitPosition(key, bit, start, end, indexType, flags);
	}

	public long StringDecrement(RedisKey key, long value, CommandFlags flags)
	{
		return _inner.StringDecrement(key, value, flags);
	}

	public bool StringDelete(RedisKey key, ValueCondition when, CommandFlags flags)
	{
		return _inner.StringDelete(key, when, flags);
	}

	public double StringDecrement(RedisKey key, double value, CommandFlags flags)
	{
		return _inner.StringDecrement(key, value, flags);
	}

	public ValueCondition? StringDigest(RedisKey key, CommandFlags flags)
	{
		return _inner.StringDigest(key, flags);
	}

	public GcraRateLimitResult StringGcraRateLimit(RedisKey key, int maxBurst, int requestsPerPeriod, double periodSeconds, int count, CommandFlags flags)
	{
		return _inner.StringGcraRateLimit(key, maxBurst, requestsPerPeriod, periodSeconds, count, flags);
	}

	public RedisValue StringGet(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGet(key, flags);
	}

	public RedisValue[] StringGet(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.StringGet(keys, flags);
	}

	public Lease<byte> StringGetLease(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetLease(key, flags);
	}

	public bool StringGetBit(RedisKey key, long offset, CommandFlags flags)
	{
		return _inner.StringGetBit(key, offset, flags);
	}

	public RedisValue StringGetRange(RedisKey key, long start, long end, CommandFlags flags)
	{
		return _inner.StringGetRange(key, start, end, flags);
	}

	public RedisValue StringGetSet(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.StringGetSet(key, value, flags);
	}

	public RedisValue StringGetSetExpiry(RedisKey key, TimeSpan? expiry, CommandFlags flags)
	{
		return _inner.StringGetSetExpiry(key, expiry, flags);
	}

	public RedisValue StringGetSetExpiry(RedisKey key, DateTime expiry, CommandFlags flags)
	{
		return _inner.StringGetSetExpiry(key, expiry, flags);
	}

	public RedisValue StringGetDelete(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetDelete(key, flags);
	}

	public RedisValueWithExpiry StringGetWithExpiry(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetWithExpiry(key, flags);
	}

	public long StringIncrement(RedisKey key, long value, CommandFlags flags)
	{
		return _inner.StringIncrement(key, value, flags);
	}

	public double StringIncrement(RedisKey key, double value, CommandFlags flags)
	{
		return _inner.StringIncrement(key, value, flags);
	}

	public long StringLength(RedisKey key, CommandFlags flags)
	{
		return _inner.StringLength(key, flags);
	}

	public string StringLongestCommonSubsequence(RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequence(first, second, flags);
	}

	public long StringLongestCommonSubsequenceLength(RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequenceLength(first, second, flags);
	}

	public LCSMatchResult StringLongestCommonSubsequenceWithMatches(RedisKey first, RedisKey second, long minLength, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequenceWithMatches(first, second, minLength, flags);
	}

	public bool StringSet(RedisKey key, RedisValue value, TimeSpan? expiry, When when)
	{
		return _inner.StringSet(key, value, expiry, when);
	}

	public bool StringSet(RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags)
	{
		return _inner.StringSet(key, value, expiry, when, flags);
	}

	public bool StringSet(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.StringSet(key, value, expiry, keepTtl, when, flags);
	}

	public bool StringSet(RedisKey key, RedisValue value, Expiration expiry, ValueCondition when, CommandFlags flags)
	{
		return _inner.StringSet(key, value, expiry, when, flags);
	}

	public bool StringSet(KeyValuePair<RedisKey, RedisValue>[] values, When when, CommandFlags flags)
	{
		return _inner.StringSet(values, when, flags);
	}

	public bool StringSet(KeyValuePair<RedisKey, RedisValue>[] values, When when, Expiration expiry, CommandFlags flags)
	{
		return _inner.StringSet(values, when, expiry, flags);
	}

	public RedisValue StringSetAndGet(RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags)
	{
		return _inner.StringSetAndGet(key, value, expiry, when, flags);
	}

	public RedisValue StringSetAndGet(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.StringSetAndGet(key, value, expiry, keepTtl, when, flags);
	}

	public bool StringSetBit(RedisKey key, long offset, bool bit, CommandFlags flags)
	{
		return _inner.StringSetBit(key, offset, bit, flags);
	}

	public RedisValue StringSetRange(RedisKey key, long offset, RedisValue value, CommandFlags flags)
	{
		return _inner.StringSetRange(key, offset, value, flags);
	}

	public bool VectorSetAdd(RedisKey key, VectorSetAddRequest request, CommandFlags flags)
	{
		return _inner.VectorSetAdd(key, request, flags);
	}

	public long VectorSetLength(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetLength(key, flags);
	}

	public int VectorSetDimension(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetDimension(key, flags);
	}

	public Lease<float> VectorSetGetApproximateVector(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetApproximateVector(key, member, flags);
	}

	public string VectorSetGetAttributesJson(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetAttributesJson(key, member, flags);
	}

	public VectorSetInfo? VectorSetInfo(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetInfo(key, flags);
	}

	public bool VectorSetContains(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetContains(key, member, flags);
	}

	public Lease<RedisValue> VectorSetGetLinks(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetLinks(key, member, flags);
	}

	public Lease<VectorSetLink> VectorSetGetLinksWithScores(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetLinksWithScores(key, member, flags);
	}

	public RedisValue VectorSetRandomMember(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetRandomMember(key, flags);
	}

	public RedisValue[] VectorSetRandomMembers(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.VectorSetRandomMembers(key, count, flags);
	}

	public bool VectorSetRemove(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetRemove(key, member, flags);
	}

	public bool VectorSetSetAttributesJson(RedisKey key, RedisValue member, string attributesJson, CommandFlags flags)
	{
		return _inner.VectorSetSetAttributesJson(key, member, attributesJson, flags);
	}

	public Lease<VectorSetSimilaritySearchResult> VectorSetSimilaritySearch(RedisKey key, VectorSetSimilaritySearchRequest query, CommandFlags flags)
	{
		return _inner.VectorSetSimilaritySearch(key, query, flags);
	}

	public Lease<RedisValue> VectorSetRange(RedisKey key, RedisValue start, RedisValue end, long count, Exclude exclude, CommandFlags flags)
	{
		return _inner.VectorSetRange(key, start, end, count, exclude, flags);
	}

	public IEnumerable<RedisValue> VectorSetRangeEnumerate(RedisKey key, RedisValue start, RedisValue end, long count, Exclude exclude, CommandFlags flags)
	{
		return _inner.VectorSetRangeEnumerate(key, start, end, count, exclude, flags);
	}

	public TimeSpan Ping(CommandFlags flags)
	{
		return _inner.Ping(flags);
	}

	public Task<TimeSpan> PingAsync(CommandFlags flags)
	{
		return _inner.PingAsync(flags);
	}

	public bool TryWait(Task task)
	{
		return _inner.TryWait(task);
	}

	public void Wait(Task task)
	{
		_inner.Wait(task);
	}

	public T Wait<T>(Task<T> task)
	{
		return _inner.Wait(task);
	}

	public void WaitAll(Task[] tasks)
	{
		_inner.WaitAll(tasks);
	}

	public bool IsConnected(RedisKey key, CommandFlags flags)
	{
		return _inner.IsConnected(key, flags);
	}

	public Task KeyMigrateAsync(RedisKey key, EndPoint toServer, int toDatabase, int timeoutMilliseconds, MigrateOptions migrateOptions, CommandFlags flags)
	{
		return _inner.KeyMigrateAsync(key, toServer, toDatabase, timeoutMilliseconds, migrateOptions, flags);
	}

	public Task<RedisValue> DebugObjectAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.DebugObjectAsync(key, flags);
	}

	public Task<bool> GeoAddAsync(RedisKey key, double longitude, double latitude, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoAddAsync(key, longitude, latitude, member, flags);
	}

	public Task<bool> GeoAddAsync(RedisKey key, GeoEntry value, CommandFlags flags)
	{
		return _inner.GeoAddAsync(key, value, flags);
	}

	public Task<long> GeoAddAsync(RedisKey key, GeoEntry[] values, CommandFlags flags)
	{
		return _inner.GeoAddAsync(key, values, flags);
	}

	public Task<bool> GeoRemoveAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoRemoveAsync(key, member, flags);
	}

	public Task<double?> GeoDistanceAsync(RedisKey key, RedisValue member1, RedisValue member2, GeoUnit unit, CommandFlags flags)
	{
		return _inner.GeoDistanceAsync(key, member1, member2, unit, flags);
	}

	public Task<string[]> GeoHashAsync(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.GeoHashAsync(key, members, flags);
	}

	public Task<string> GeoHashAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoHashAsync(key, member, flags);
	}

	public Task<GeoPosition?[]> GeoPositionAsync(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.GeoPositionAsync(key, members, flags);
	}

	public Task<GeoPosition?> GeoPositionAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.GeoPositionAsync(key, member, flags);
	}

	public Task<GeoRadiusResult[]> GeoRadiusAsync(RedisKey key, RedisValue member, double radius, GeoUnit unit, int count, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoRadiusAsync(key, member, radius, unit, count, order, options, flags);
	}

	public Task<GeoRadiusResult[]> GeoRadiusAsync(RedisKey key, double longitude, double latitude, double radius, GeoUnit unit, int count, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoRadiusAsync(key, longitude, latitude, radius, unit, count, order, options, flags);
	}

	public Task<GeoRadiusResult[]> GeoSearchAsync(RedisKey key, RedisValue member, GeoSearchShape shape, int count, bool demandClosest, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoSearchAsync(key, member, shape, count, demandClosest, order, options, flags);
	}

	public Task<GeoRadiusResult[]> GeoSearchAsync(RedisKey key, double longitude, double latitude, GeoSearchShape shape, int count, bool demandClosest, Order? order, GeoRadiusOptions options, CommandFlags flags)
	{
		return _inner.GeoSearchAsync(key, longitude, latitude, shape, count, demandClosest, order, options, flags);
	}

	public Task<long> GeoSearchAndStoreAsync(RedisKey sourceKey, RedisKey destinationKey, RedisValue member, GeoSearchShape shape, int count, bool demandClosest, Order? order, bool storeDistances, CommandFlags flags)
	{
		return _inner.GeoSearchAndStoreAsync(sourceKey, destinationKey, member, shape, count, demandClosest, order, storeDistances, flags);
	}

	public Task<long> GeoSearchAndStoreAsync(RedisKey sourceKey, RedisKey destinationKey, double longitude, double latitude, GeoSearchShape shape, int count, bool demandClosest, Order? order, bool storeDistances, CommandFlags flags)
	{
		return _inner.GeoSearchAndStoreAsync(sourceKey, destinationKey, longitude, latitude, shape, count, demandClosest, order, storeDistances, flags);
	}

	public Task<long> HashDecrementAsync(RedisKey key, RedisValue hashField, long value, CommandFlags flags)
	{
		return _inner.HashDecrementAsync(key, hashField, value, flags);
	}

	public Task<double> HashDecrementAsync(RedisKey key, RedisValue hashField, double value, CommandFlags flags)
	{
		return _inner.HashDecrementAsync(key, hashField, value, flags);
	}

	public Task<bool> HashDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashDeleteAsync(key, hashField, flags);
	}

	public Task<long> HashDeleteAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashDeleteAsync(key, hashFields, flags);
	}

	public Task<bool> HashExistsAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashExistsAsync(key, hashField, flags);
	}

	public Task<RedisValue> HashFieldGetAndDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashFieldGetAndDeleteAsync(key, hashField, flags);
	}

	public Task<Lease<byte>> HashFieldGetLeaseAndDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndDeleteAsync(key, hashField, flags);
	}

	public Task<RedisValue[]> HashFieldGetAndDeleteAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetAndDeleteAsync(key, hashFields, flags);
	}

	public Task<RedisValue> HashFieldGetAndSetExpiryAsync(RedisKey key, RedisValue hashField, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiryAsync(key, hashField, expiry, persist, flags);
	}

	public Task<RedisValue> HashFieldGetAndSetExpiryAsync(RedisKey key, RedisValue hashField, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiryAsync(key, hashField, expiry, flags);
	}

	public Task<Lease<byte>> HashFieldGetLeaseAndSetExpiryAsync(RedisKey key, RedisValue hashField, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndSetExpiryAsync(key, hashField, expiry, persist, flags);
	}

	public Task<Lease<byte>> HashFieldGetLeaseAndSetExpiryAsync(RedisKey key, RedisValue hashField, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetLeaseAndSetExpiryAsync(key, hashField, expiry, flags);
	}

	public Task<RedisValue[]> HashFieldGetAndSetExpiryAsync(RedisKey key, RedisValue[] hashFields, TimeSpan? expiry, bool persist, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiryAsync(key, hashFields, expiry, persist, flags);
	}

	public Task<RedisValue[]> HashFieldGetAndSetExpiryAsync(RedisKey key, RedisValue[] hashFields, DateTime expiry, CommandFlags flags)
	{
		return _inner.HashFieldGetAndSetExpiryAsync(key, hashFields, expiry, flags);
	}

	public Task<RedisValue> HashFieldSetAndSetExpiryAsync(RedisKey key, RedisValue field, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiryAsync(key, field, value, expiry, keepTtl, when, flags);
	}

	public Task<RedisValue> HashFieldSetAndSetExpiryAsync(RedisKey key, RedisValue field, RedisValue value, DateTime expiry, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiryAsync(key, field, value, expiry, when, flags);
	}

	public Task<RedisValue> HashFieldSetAndSetExpiryAsync(RedisKey key, HashEntry[] hashFields, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiryAsync(key, hashFields, expiry, keepTtl, when, flags);
	}

	public Task<RedisValue> HashFieldSetAndSetExpiryAsync(RedisKey key, HashEntry[] hashFields, DateTime expiry, When when, CommandFlags flags)
	{
		return _inner.HashFieldSetAndSetExpiryAsync(key, hashFields, expiry, when, flags);
	}

	public Task<ExpireResult[]> HashFieldExpireAsync(RedisKey key, RedisValue[] hashFields, TimeSpan expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.HashFieldExpireAsync(key, hashFields, expiry, when, flags);
	}

	public Task<ExpireResult[]> HashFieldExpireAsync(RedisKey key, RedisValue[] hashFields, DateTime expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.HashFieldExpireAsync(key, hashFields, expiry, when, flags);
	}

	public Task<long[]> HashFieldGetExpireDateTimeAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetExpireDateTimeAsync(key, hashFields, flags);
	}

	public Task<PersistResult[]> HashFieldPersistAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldPersistAsync(key, hashFields, flags);
	}

	public Task<long[]> HashFieldGetTimeToLiveAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashFieldGetTimeToLiveAsync(key, hashFields, flags);
	}

	public Task<RedisValue> HashGetAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashGetAsync(key, hashField, flags);
	}

	public Task<Lease<byte>> HashGetLeaseAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashGetLeaseAsync(key, hashField, flags);
	}

	public Task<RedisValue[]> HashGetAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags)
	{
		return _inner.HashGetAsync(key, hashFields, flags);
	}

	public Task<HashEntry[]> HashGetAllAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HashGetAllAsync(key, flags);
	}

	public Task<long> HashIncrementAsync(RedisKey key, RedisValue hashField, long value, CommandFlags flags)
	{
		return _inner.HashIncrementAsync(key, hashField, value, flags);
	}

	public Task<double> HashIncrementAsync(RedisKey key, RedisValue hashField, double value, CommandFlags flags)
	{
		return _inner.HashIncrementAsync(key, hashField, value, flags);
	}

	public Task<RedisValue[]> HashKeysAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HashKeysAsync(key, flags);
	}

	public Task<long> HashLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HashLengthAsync(key, flags);
	}

	public Task<RedisValue> HashRandomFieldAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HashRandomFieldAsync(key, flags);
	}

	public Task<RedisValue[]> HashRandomFieldsAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.HashRandomFieldsAsync(key, count, flags);
	}

	public Task<HashEntry[]> HashRandomFieldsWithValuesAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.HashRandomFieldsWithValuesAsync(key, count, flags);
	}

	public IAsyncEnumerable<HashEntry> HashScanAsync(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.HashScanAsync(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public IAsyncEnumerable<RedisValue> HashScanNoValuesAsync(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.HashScanNoValuesAsync(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public Task HashSetAsync(RedisKey key, HashEntry[] hashFields, CommandFlags flags)
	{
		return _inner.HashSetAsync(key, hashFields, flags);
	}

	public Task<bool> HashSetAsync(RedisKey key, RedisValue hashField, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.HashSetAsync(key, hashField, value, when, flags);
	}

	public Task<long> HashStringLengthAsync(RedisKey key, RedisValue hashField, CommandFlags flags)
	{
		return _inner.HashStringLengthAsync(key, hashField, flags);
	}

	public Task<RedisValue[]> HashValuesAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HashValuesAsync(key, flags);
	}

	public Task<bool> HyperLogLogAddAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.HyperLogLogAddAsync(key, value, flags);
	}

	public Task<bool> HyperLogLogAddAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.HyperLogLogAddAsync(key, values, flags);
	}

	public Task<long> HyperLogLogLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.HyperLogLogLengthAsync(key, flags);
	}

	public Task<long> HyperLogLogLengthAsync(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.HyperLogLogLengthAsync(keys, flags);
	}

	public Task HyperLogLogMergeAsync(RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.HyperLogLogMergeAsync(destination, first, second, flags);
	}

	public Task HyperLogLogMergeAsync(RedisKey destination, RedisKey[] sourceKeys, CommandFlags flags)
	{
		return _inner.HyperLogLogMergeAsync(destination, sourceKeys, flags);
	}

	public Task<EndPoint> IdentifyEndpointAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.IdentifyEndpointAsync(key, flags);
	}

	public Task<bool> KeyCopyAsync(RedisKey sourceKey, RedisKey destinationKey, int destinationDatabase, bool replace, CommandFlags flags)
	{
		return _inner.KeyCopyAsync(sourceKey, destinationKey, destinationDatabase, replace, flags);
	}

	public Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyDeleteAsync(key, flags);
	}

	public Task<long> KeyDeleteAsync(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyDeleteAsync(keys, flags);
	}

	public Task<byte[]> KeyDumpAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyDumpAsync(key, flags);
	}

	public Task<string> KeyEncodingAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyEncodingAsync(key, flags);
	}

	public Task<bool> KeyExistsAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyExistsAsync(key, flags);
	}

	public Task<long> KeyExistsAsync(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyExistsAsync(keys, flags);
	}

	public Task<bool> KeyExpireAsync(RedisKey key, TimeSpan? expiry, CommandFlags flags)
	{
		return _inner.KeyExpireAsync(key, expiry, flags);
	}

	public Task<bool> KeyExpireAsync(RedisKey key, TimeSpan? expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.KeyExpireAsync(key, expiry, when, flags);
	}

	public Task<bool> KeyExpireAsync(RedisKey key, DateTime? expiry, CommandFlags flags)
	{
		return _inner.KeyExpireAsync(key, expiry, flags);
	}

	public Task<bool> KeyExpireAsync(RedisKey key, DateTime? expiry, ExpireWhen when, CommandFlags flags)
	{
		return _inner.KeyExpireAsync(key, expiry, when, flags);
	}

	public Task<DateTime?> KeyExpireTimeAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyExpireTimeAsync(key, flags);
	}

	public Task<long?> KeyFrequencyAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyFrequencyAsync(key, flags);
	}

	public Task<TimeSpan?> KeyIdleTimeAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyIdleTimeAsync(key, flags);
	}

	public Task<bool> KeyMoveAsync(RedisKey key, int database, CommandFlags flags)
	{
		return _inner.KeyMoveAsync(key, database, flags);
	}

	public Task<bool> KeyPersistAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyPersistAsync(key, flags);
	}

	public Task<RedisKey> KeyRandomAsync(CommandFlags flags)
	{
		return _inner.KeyRandomAsync(flags);
	}

	public Task<long?> KeyRefCountAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyRefCountAsync(key, flags);
	}

	public Task<bool> KeyRenameAsync(RedisKey key, RedisKey newKey, When when, CommandFlags flags)
	{
		return _inner.KeyRenameAsync(key, newKey, when, flags);
	}

	public Task KeyRestoreAsync(RedisKey key, byte[] value, TimeSpan? expiry, CommandFlags flags)
	{
		return _inner.KeyRestoreAsync(key, value, expiry, flags);
	}

	public Task<TimeSpan?> KeyTimeToLiveAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyTimeToLiveAsync(key, flags);
	}

	public Task<bool> KeyTouchAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyTouchAsync(key, flags);
	}

	public Task<long> KeyTouchAsync(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.KeyTouchAsync(keys, flags);
	}

	public Task<RedisType> KeyTypeAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.KeyTypeAsync(key, flags);
	}

	public Task<RedisValue> ListGetByIndexAsync(RedisKey key, long index, CommandFlags flags)
	{
		return _inner.ListGetByIndexAsync(key, index, flags);
	}

	public Task<long> ListInsertAfterAsync(RedisKey key, RedisValue pivot, RedisValue value, CommandFlags flags)
	{
		return _inner.ListInsertAfterAsync(key, pivot, value, flags);
	}

	public Task<long> ListInsertBeforeAsync(RedisKey key, RedisValue pivot, RedisValue value, CommandFlags flags)
	{
		return _inner.ListInsertBeforeAsync(key, pivot, value, flags);
	}

	public Task<RedisValue> ListLeftPopAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.ListLeftPopAsync(key, flags);
	}

	public Task<RedisValue[]> ListLeftPopAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.ListLeftPopAsync(key, count, flags);
	}

	public Task<ListPopResult> ListLeftPopAsync(RedisKey[] keys, long count, CommandFlags flags)
	{
		return _inner.ListLeftPopAsync(keys, count, flags);
	}

	public Task<long> ListPositionAsync(RedisKey key, RedisValue element, long rank, long maxLength, CommandFlags flags)
	{
		return _inner.ListPositionAsync(key, element, rank, maxLength, flags);
	}

	public Task<long[]> ListPositionsAsync(RedisKey key, RedisValue element, long count, long rank, long maxLength, CommandFlags flags)
	{
		return _inner.ListPositionsAsync(key, element, count, rank, maxLength, flags);
	}

	public Task<long> ListLeftPushAsync(RedisKey key, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.ListLeftPushAsync(key, value, when, flags);
	}

	public Task<long> ListLeftPushAsync(RedisKey key, RedisValue[] values, When when, CommandFlags flags)
	{
		return _inner.ListLeftPushAsync(key, values, when, flags);
	}

	public Task<long> ListLeftPushAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ListLeftPushAsync(key, values, flags);
	}

	public Task<long> ListLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.ListLengthAsync(key, flags);
	}

	public Task<RedisValue> ListMoveAsync(RedisKey sourceKey, RedisKey destinationKey, ListSide sourceSide, ListSide destinationSide, CommandFlags flags)
	{
		return _inner.ListMoveAsync(sourceKey, destinationKey, sourceSide, destinationSide, flags);
	}

	public Task<RedisValue[]> ListRangeAsync(RedisKey key, long start, long stop, CommandFlags flags)
	{
		return _inner.ListRangeAsync(key, start, stop, flags);
	}

	public Task<long> ListRemoveAsync(RedisKey key, RedisValue value, long count, CommandFlags flags)
	{
		return _inner.ListRemoveAsync(key, value, count, flags);
	}

	public Task<RedisValue> ListRightPopAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.ListRightPopAsync(key, flags);
	}

	public Task<RedisValue[]> ListRightPopAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.ListRightPopAsync(key, count, flags);
	}

	public Task<ListPopResult> ListRightPopAsync(RedisKey[] keys, long count, CommandFlags flags)
	{
		return _inner.ListRightPopAsync(keys, count, flags);
	}

	public Task<RedisValue> ListRightPopLeftPushAsync(RedisKey source, RedisKey destination, CommandFlags flags)
	{
		return _inner.ListRightPopLeftPushAsync(source, destination, flags);
	}

	public Task<long> ListRightPushAsync(RedisKey key, RedisValue value, When when, CommandFlags flags)
	{
		return _inner.ListRightPushAsync(key, value, when, flags);
	}

	public Task<long> ListRightPushAsync(RedisKey key, RedisValue[] values, When when, CommandFlags flags)
	{
		return _inner.ListRightPushAsync(key, values, when, flags);
	}

	public Task<long> ListRightPushAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ListRightPushAsync(key, values, flags);
	}

	public Task ListSetByIndexAsync(RedisKey key, long index, RedisValue value, CommandFlags flags)
	{
		return _inner.ListSetByIndexAsync(key, index, value, flags);
	}

	public Task ListTrimAsync(RedisKey key, long start, long stop, CommandFlags flags)
	{
		return _inner.ListTrimAsync(key, start, stop, flags);
	}

	public Task<bool> LockExtendAsync(RedisKey key, RedisValue value, TimeSpan expiry, CommandFlags flags)
	{
		return _inner.LockExtendAsync(key, value, expiry, flags);
	}

	public Task<RedisValue> LockQueryAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.LockQueryAsync(key, flags);
	}

	public Task<bool> LockReleaseAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.LockReleaseAsync(key, value, flags);
	}

	public Task<bool> LockTakeAsync(RedisKey key, RedisValue value, TimeSpan expiry, CommandFlags flags)
	{
		return _inner.LockTakeAsync(key, value, expiry, flags);
	}

	public Task<long> PublishAsync(RedisChannel channel, RedisValue message, CommandFlags flags)
	{
		return _inner.PublishAsync(channel, message, flags);
	}

	public Task<RedisResult> ExecuteAsync(string command, object[] args)
	{
		return _inner.ExecuteAsync(command, args);
	}

	public Task<RedisResult> ExecuteAsync(string command, ICollection<object> args, CommandFlags flags)
	{
		return _inner.ExecuteAsync(command, args, flags);
	}

	public Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateAsync(script, keys, values, flags);
	}

	public Task<RedisResult> ScriptEvaluateAsync(byte[] hash, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateAsync(hash, keys, values, flags);
	}

	public Task<RedisResult> ScriptEvaluateAsync(LuaScript script, object parameters, CommandFlags flags)
	{
		return _inner.ScriptEvaluateAsync(script, parameters, flags);
	}

	public Task<RedisResult> ScriptEvaluateAsync(LoadedLuaScript script, object parameters, CommandFlags flags)
	{
		return _inner.ScriptEvaluateAsync(script, parameters, flags);
	}

	public Task<RedisResult> ScriptEvaluateReadOnlyAsync(string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateReadOnlyAsync(script, keys, values, flags);
	}

	public Task<RedisResult> ScriptEvaluateReadOnlyAsync(byte[] hash, RedisKey[] keys, RedisValue[] values, CommandFlags flags)
	{
		return _inner.ScriptEvaluateReadOnlyAsync(hash, keys, values, flags);
	}

	public Task<bool> SetAddAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetAddAsync(key, value, flags);
	}

	public Task<long> SetAddAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetAddAsync(key, values, flags);
	}

	public Task<RedisValue[]> SetCombineAsync(SetOperation operation, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.SetCombineAsync(operation, first, second, flags);
	}

	public Task<RedisValue[]> SetCombineAsync(SetOperation operation, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.SetCombineAsync(operation, keys, flags);
	}

	public Task<long> SetCombineAndStoreAsync(SetOperation operation, RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.SetCombineAndStoreAsync(operation, destination, first, second, flags);
	}

	public Task<long> SetCombineAndStoreAsync(SetOperation operation, RedisKey destination, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.SetCombineAndStoreAsync(operation, destination, keys, flags);
	}

	public Task<bool> SetContainsAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetContainsAsync(key, value, flags);
	}

	public Task<bool[]> SetContainsAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetContainsAsync(key, values, flags);
	}

	public Task<long> SetIntersectionLengthAsync(RedisKey[] keys, long limit, CommandFlags flags)
	{
		return _inner.SetIntersectionLengthAsync(keys, limit, flags);
	}

	public Task<long> SetLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.SetLengthAsync(key, flags);
	}

	public Task<RedisValue[]> SetMembersAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.SetMembersAsync(key, flags);
	}

	public Task<bool> SetMoveAsync(RedisKey source, RedisKey destination, RedisValue value, CommandFlags flags)
	{
		return _inner.SetMoveAsync(source, destination, value, flags);
	}

	public Task<RedisValue> SetPopAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.SetPopAsync(key, flags);
	}

	public Task<RedisValue[]> SetPopAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SetPopAsync(key, count, flags);
	}

	public Task<RedisValue> SetRandomMemberAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.SetRandomMemberAsync(key, flags);
	}

	public Task<RedisValue[]> SetRandomMembersAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SetRandomMembersAsync(key, count, flags);
	}

	public Task<bool> SetRemoveAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.SetRemoveAsync(key, value, flags);
	}

	public Task<long> SetRemoveAsync(RedisKey key, RedisValue[] values, CommandFlags flags)
	{
		return _inner.SetRemoveAsync(key, values, flags);
	}

	public IAsyncEnumerable<RedisValue> SetScanAsync(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.SetScanAsync(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public Task<RedisValue[]> SortAsync(RedisKey key, long skip, long take, Order order, SortType sortType, RedisValue by, RedisValue[] get, CommandFlags flags)
	{
		return _inner.SortAsync(key, skip, take, order, sortType, by, get, flags);
	}

	public Task<long> SortAndStoreAsync(RedisKey destination, RedisKey key, long skip, long take, Order order, SortType sortType, RedisValue by, RedisValue[] get, CommandFlags flags)
	{
		return _inner.SortAndStoreAsync(destination, key, skip, take, order, sortType, by, get, flags);
	}

	public Task<bool> SortedSetAddAsync(RedisKey key, RedisValue member, double score, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, member, score, flags);
	}

	public Task<bool> SortedSetAddAsync(RedisKey key, RedisValue member, double score, When when, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, member, score, when, flags);
	}

	public Task<bool> SortedSetAddAsync(RedisKey key, RedisValue member, double score, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, member, score, when, flags);
	}

	public Task<long> SortedSetAddAsync(RedisKey key, SortedSetEntry[] values, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, values, flags);
	}

	public Task<long> SortedSetAddAsync(RedisKey key, SortedSetEntry[] values, When when, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, values, when, flags);
	}

	public Task<long> SortedSetAddAsync(RedisKey key, SortedSetEntry[] values, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetAddAsync(key, values, when, flags);
	}

	public Task<RedisValue[]> SortedSetCombineAsync(SetOperation operation, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineAsync(operation, keys, weights, aggregate, flags);
	}

	public Task<SortedSetEntry[]> SortedSetCombineWithScoresAsync(SetOperation operation, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineWithScoresAsync(operation, keys, weights, aggregate, flags);
	}

	public Task<long> SortedSetCombineAndStoreAsync(SetOperation operation, RedisKey destination, RedisKey first, RedisKey second, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineAndStoreAsync(operation, destination, first, second, aggregate, flags);
	}

	public Task<long> SortedSetCombineAndStoreAsync(SetOperation operation, RedisKey destination, RedisKey[] keys, double[] weights, Aggregate aggregate, CommandFlags flags)
	{
		return _inner.SortedSetCombineAndStoreAsync(operation, destination, keys, weights, aggregate, flags);
	}

	public Task<double> SortedSetDecrementAsync(RedisKey key, RedisValue member, double value, CommandFlags flags)
	{
		return _inner.SortedSetDecrementAsync(key, member, value, flags);
	}

	public Task<double> SortedSetIncrementAsync(RedisKey key, RedisValue member, double value, CommandFlags flags)
	{
		return _inner.SortedSetIncrementAsync(key, member, value, flags);
	}

	public Task<long> SortedSetIntersectionLengthAsync(RedisKey[] keys, long limit, CommandFlags flags)
	{
		return _inner.SortedSetIntersectionLengthAsync(keys, limit, flags);
	}

	public Task<long> SortedSetLengthAsync(RedisKey key, double min, double max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetLengthAsync(key, min, max, exclude, flags);
	}

	public Task<long> SortedSetLengthByValueAsync(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetLengthByValueAsync(key, min, max, exclude, flags);
	}

	public Task<RedisValue> SortedSetRandomMemberAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.SortedSetRandomMemberAsync(key, flags);
	}

	public Task<RedisValue[]> SortedSetRandomMembersAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SortedSetRandomMembersAsync(key, count, flags);
	}

	public Task<SortedSetEntry[]> SortedSetRandomMembersWithScoresAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.SortedSetRandomMembersWithScoresAsync(key, count, flags);
	}

	public Task<RedisValue[]> SortedSetRangeByRankAsync(RedisKey key, long start, long stop, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRangeByRankAsync(key, start, stop, order, flags);
	}

	public Task<long> SortedSetRangeAndStoreAsync(RedisKey sourceKey, RedisKey destinationKey, RedisValue start, RedisValue stop, SortedSetOrder sortedSetOrder, Exclude exclude, Order order, long skip, long? take, CommandFlags flags)
	{
		return _inner.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, start, stop, sortedSetOrder, exclude, order, skip, take, flags);
	}

	public Task<SortedSetEntry[]> SortedSetRangeByRankWithScoresAsync(RedisKey key, long start, long stop, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRangeByRankWithScoresAsync(key, start, stop, order, flags);
	}

	public Task<RedisValue[]> SortedSetRangeByScoreAsync(RedisKey key, double start, double stop, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByScoreAsync(key, start, stop, exclude, order, skip, take, flags);
	}

	public Task<SortedSetEntry[]> SortedSetRangeByScoreWithScoresAsync(RedisKey key, double start, double stop, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByScoreWithScoresAsync(key, start, stop, exclude, order, skip, take, flags);
	}

	public Task<RedisValue[]> SortedSetRangeByValueAsync(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByValueAsync(key, min, max, exclude, skip, take, flags);
	}

	public Task<RedisValue[]> SortedSetRangeByValueAsync(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, Order order, long skip, long take, CommandFlags flags)
	{
		return _inner.SortedSetRangeByValueAsync(key, min, max, exclude, order, skip, take, flags);
	}

	public Task<long?> SortedSetRankAsync(RedisKey key, RedisValue member, Order order, CommandFlags flags)
	{
		return _inner.SortedSetRankAsync(key, member, order, flags);
	}

	public Task<bool> SortedSetRemoveAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.SortedSetRemoveAsync(key, member, flags);
	}

	public Task<long> SortedSetRemoveAsync(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.SortedSetRemoveAsync(key, members, flags);
	}

	public Task<long> SortedSetRemoveRangeByRankAsync(RedisKey key, long start, long stop, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByRankAsync(key, start, stop, flags);
	}

	public Task<long> SortedSetRemoveRangeByScoreAsync(RedisKey key, double start, double stop, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByScoreAsync(key, start, stop, exclude, flags);
	}

	public Task<long> SortedSetRemoveRangeByValueAsync(RedisKey key, RedisValue min, RedisValue max, Exclude exclude, CommandFlags flags)
	{
		return _inner.SortedSetRemoveRangeByValueAsync(key, min, max, exclude, flags);
	}

	public IAsyncEnumerable<SortedSetEntry> SortedSetScanAsync(RedisKey key, RedisValue pattern, int pageSize, long cursor, int pageOffset, CommandFlags flags)
	{
		return _inner.SortedSetScanAsync(key, pattern, pageSize, cursor, pageOffset, flags);
	}

	public Task<double?> SortedSetScoreAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.SortedSetScoreAsync(key, member, flags);
	}

	public Task<double?[]> SortedSetScoresAsync(RedisKey key, RedisValue[] members, CommandFlags flags)
	{
		return _inner.SortedSetScoresAsync(key, members, flags);
	}

	public Task<bool> SortedSetUpdateAsync(RedisKey key, RedisValue member, double score, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetUpdateAsync(key, member, score, when, flags);
	}

	public Task<long> SortedSetUpdateAsync(RedisKey key, SortedSetEntry[] values, SortedSetWhen when, CommandFlags flags)
	{
		return _inner.SortedSetUpdateAsync(key, values, when, flags);
	}

	public Task<SortedSetEntry?> SortedSetPopAsync(RedisKey key, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPopAsync(key, order, flags);
	}

	public Task<SortedSetEntry[]> SortedSetPopAsync(RedisKey key, long count, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPopAsync(key, count, order, flags);
	}

	public Task<SortedSetPopResult> SortedSetPopAsync(RedisKey[] keys, long count, Order order, CommandFlags flags)
	{
		return _inner.SortedSetPopAsync(keys, count, order, flags);
	}

	public Task<long> StreamAcknowledgeAsync(RedisKey key, RedisValue groupName, RedisValue messageId, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAsync(key, groupName, messageId, flags);
	}

	public Task<long> StreamAcknowledgeAsync(RedisKey key, RedisValue groupName, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAsync(key, groupName, messageIds, flags);
	}

	public Task<StreamTrimResult> StreamAcknowledgeAndDeleteAsync(RedisKey key, RedisValue groupName, StreamTrimMode mode, RedisValue messageId, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAndDeleteAsync(key, groupName, mode, messageId, flags);
	}

	public Task<StreamTrimResult[]> StreamAcknowledgeAndDeleteAsync(RedisKey key, RedisValue groupName, StreamTrimMode mode, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamAcknowledgeAndDeleteAsync(key, groupName, mode, messageIds, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, RedisValue streamField, RedisValue streamValue, RedisValue? messageId, int? maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamField, streamValue, messageId, maxLength, useApproximateMaxLength, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, NameValueEntry[] streamPairs, RedisValue? messageId, int? maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamPairs, messageId, maxLength, useApproximateMaxLength, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, RedisValue streamField, RedisValue streamValue, RedisValue? messageId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamField, streamValue, messageId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, NameValueEntry[] streamPairs, RedisValue? messageId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamPairs, messageId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, RedisValue streamField, RedisValue streamValue, StreamIdempotentId idempotentId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamField, streamValue, idempotentId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public Task<RedisValue> StreamAddAsync(RedisKey key, NameValueEntry[] streamPairs, StreamIdempotentId idempotentId, long? maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode trimMode, CommandFlags flags)
	{
		return _inner.StreamAddAsync(key, streamPairs, idempotentId, maxLength, useApproximateMaxLength, limit, trimMode, flags);
	}

	public Task StreamConfigureAsync(RedisKey key, StreamConfiguration configuration, CommandFlags flags)
	{
		return _inner.StreamConfigureAsync(key, configuration, flags);
	}

	public Task<StreamAutoClaimResult> StreamAutoClaimAsync(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue startAtId, int? count, CommandFlags flags)
	{
		return _inner.StreamAutoClaimAsync(key, consumerGroup, claimingConsumer, minIdleTimeInMs, startAtId, count, flags);
	}

	public Task<StreamAutoClaimIdsOnlyResult> StreamAutoClaimIdsOnlyAsync(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue startAtId, int? count, CommandFlags flags)
	{
		return _inner.StreamAutoClaimIdsOnlyAsync(key, consumerGroup, claimingConsumer, minIdleTimeInMs, startAtId, count, flags);
	}

	public Task<StreamEntry[]> StreamClaimAsync(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamClaimAsync(key, consumerGroup, claimingConsumer, minIdleTimeInMs, messageIds, flags);
	}

	public Task<RedisValue[]> StreamClaimIdsOnlyAsync(RedisKey key, RedisValue consumerGroup, RedisValue claimingConsumer, long minIdleTimeInMs, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamClaimIdsOnlyAsync(key, consumerGroup, claimingConsumer, minIdleTimeInMs, messageIds, flags);
	}

	public Task<bool> StreamConsumerGroupSetPositionAsync(RedisKey key, RedisValue groupName, RedisValue position, CommandFlags flags)
	{
		return _inner.StreamConsumerGroupSetPositionAsync(key, groupName, position, flags);
	}

	public Task<StreamConsumerInfo[]> StreamConsumerInfoAsync(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamConsumerInfoAsync(key, groupName, flags);
	}

	public Task<bool> StreamCreateConsumerGroupAsync(RedisKey key, RedisValue groupName, RedisValue? position, CommandFlags flags)
	{
		return _inner.StreamCreateConsumerGroupAsync(key, groupName, position, flags);
	}

	public Task<bool> StreamCreateConsumerGroupAsync(RedisKey key, RedisValue groupName, RedisValue? position, bool createStream, CommandFlags flags)
	{
		return _inner.StreamCreateConsumerGroupAsync(key, groupName, position, createStream, flags);
	}

	public Task<long> StreamDeleteAsync(RedisKey key, RedisValue[] messageIds, CommandFlags flags)
	{
		return _inner.StreamDeleteAsync(key, messageIds, flags);
	}

	public Task<StreamTrimResult[]> StreamDeleteAsync(RedisKey key, RedisValue[] messageIds, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamDeleteAsync(key, messageIds, mode, flags);
	}

	public Task<long> StreamDeleteConsumerAsync(RedisKey key, RedisValue groupName, RedisValue consumerName, CommandFlags flags)
	{
		return _inner.StreamDeleteConsumerAsync(key, groupName, consumerName, flags);
	}

	public Task<bool> StreamDeleteConsumerGroupAsync(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamDeleteConsumerGroupAsync(key, groupName, flags);
	}

	public Task<StreamGroupInfo[]> StreamGroupInfoAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamGroupInfoAsync(key, flags);
	}

	public Task<StreamInfo> StreamInfoAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamInfoAsync(key, flags);
	}

	public Task<long> StreamLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StreamLengthAsync(key, flags);
	}

	public Task<StreamPendingInfo> StreamPendingAsync(RedisKey key, RedisValue groupName, CommandFlags flags)
	{
		return _inner.StreamPendingAsync(key, groupName, flags);
	}

	public Task<StreamPendingMessageInfo[]> StreamPendingMessagesAsync(RedisKey key, RedisValue groupName, int count, RedisValue consumerName, RedisValue? minId, RedisValue? maxId, CommandFlags flags)
	{
		return _inner.StreamPendingMessagesAsync(key, groupName, count, consumerName, minId, maxId, flags);
	}

	public Task<StreamPendingMessageInfo[]> StreamPendingMessagesAsync(RedisKey key, RedisValue groupName, int count, RedisValue consumerName, RedisValue? minId, RedisValue? maxId, long? minIdleTimeInMs, CommandFlags flags)
	{
		return _inner.StreamPendingMessagesAsync(key, groupName, count, consumerName, minId, maxId, minIdleTimeInMs, flags);
	}

	public Task<StreamEntry[]> StreamRangeAsync(RedisKey key, RedisValue? minId, RedisValue? maxId, int? count, Order messageOrder, CommandFlags flags)
	{
		return _inner.StreamRangeAsync(key, minId, maxId, count, messageOrder, flags);
	}

	public Task<StreamEntry[]> StreamReadAsync(RedisKey key, RedisValue position, int? count, CommandFlags flags)
	{
		return _inner.StreamReadAsync(key, position, count, flags);
	}

	public Task<RedisStream[]> StreamReadAsync(StreamPosition[] streamPositions, int? countPerStream, CommandFlags flags)
	{
		return _inner.StreamReadAsync(streamPositions, countPerStream, flags);
	}

	public Task<StreamEntry[]> StreamReadGroupAsync(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(key, groupName, consumerName, position, count, flags);
	}

	public Task<StreamEntry[]> StreamReadGroupAsync(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, bool noAck, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(key, groupName, consumerName, position, count, noAck, flags);
	}

	public Task<StreamEntry[]> StreamReadGroupAsync(RedisKey key, RedisValue groupName, RedisValue consumerName, RedisValue? position, int? count, bool noAck, TimeSpan? claimMinIdleTime, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(key, groupName, consumerName, position, count, noAck, claimMinIdleTime, flags);
	}

	public Task<RedisStream[]> StreamReadGroupAsync(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(streamPositions, groupName, consumerName, countPerStream, flags);
	}

	public Task<RedisStream[]> StreamReadGroupAsync(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, bool noAck, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(streamPositions, groupName, consumerName, countPerStream, noAck, flags);
	}

	public Task<long> StreamTrimAsync(RedisKey key, int maxLength, bool useApproximateMaxLength, CommandFlags flags)
	{
		return _inner.StreamTrimAsync(key, maxLength, useApproximateMaxLength, flags);
	}

	public Task<long> StreamTrimAsync(RedisKey key, long maxLength, bool useApproximateMaxLength, long? limit, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamTrimAsync(key, maxLength, useApproximateMaxLength, limit, mode, flags);
	}

	public Task<long> StreamTrimByMinIdAsync(RedisKey key, RedisValue minId, bool useApproximateMaxLength, long? limit, StreamTrimMode mode, CommandFlags flags)
	{
		return _inner.StreamTrimByMinIdAsync(key, minId, useApproximateMaxLength, limit, mode, flags);
	}

	public Task<long> StringAppendAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.StringAppendAsync(key, value, flags);
	}

	public Task<long> StringBitCountAsync(RedisKey key, long start, long end, CommandFlags flags)
	{
		return _inner.StringBitCountAsync(key, start, end, flags);
	}

	public Task<long> StringBitCountAsync(RedisKey key, long start, long end, StringIndexType indexType, CommandFlags flags)
	{
		return _inner.StringBitCountAsync(key, start, end, indexType, flags);
	}

	public Task<long> StringBitOperationAsync(Bitwise operation, RedisKey destination, RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringBitOperationAsync(operation, destination, first, second, flags);
	}

	public Task<long> StringBitOperationAsync(Bitwise operation, RedisKey destination, RedisKey[] keys, CommandFlags flags)
	{
		return _inner.StringBitOperationAsync(operation, destination, keys, flags);
	}

	public Task<long> StringBitPositionAsync(RedisKey key, bool bit, long start, long end, CommandFlags flags)
	{
		return _inner.StringBitPositionAsync(key, bit, start, end, flags);
	}

	public Task<long> StringBitPositionAsync(RedisKey key, bool bit, long start, long end, StringIndexType indexType, CommandFlags flags)
	{
		return _inner.StringBitPositionAsync(key, bit, start, end, indexType, flags);
	}

	public Task<long> StringDecrementAsync(RedisKey key, long value, CommandFlags flags)
	{
		return _inner.StringDecrementAsync(key, value, flags);
	}

	public Task<bool> StringDeleteAsync(RedisKey key, ValueCondition when, CommandFlags flags)
	{
		return _inner.StringDeleteAsync(key, when, flags);
	}

	public Task<double> StringDecrementAsync(RedisKey key, double value, CommandFlags flags)
	{
		return _inner.StringDecrementAsync(key, value, flags);
	}

	public Task<ValueCondition?> StringDigestAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringDigestAsync(key, flags);
	}

	public Task<GcraRateLimitResult> StringGcraRateLimitAsync(RedisKey key, int maxBurst, int requestsPerPeriod, double periodSeconds, int count, CommandFlags flags)
	{
		return _inner.StringGcraRateLimitAsync(key, maxBurst, requestsPerPeriod, periodSeconds, count, flags);
	}

	public Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetAsync(key, flags);
	}

	public Task<RedisValue[]> StringGetAsync(RedisKey[] keys, CommandFlags flags)
	{
		return _inner.StringGetAsync(keys, flags);
	}

	public Task<Lease<byte>> StringGetLeaseAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetLeaseAsync(key, flags);
	}

	public Task<bool> StringGetBitAsync(RedisKey key, long offset, CommandFlags flags)
	{
		return _inner.StringGetBitAsync(key, offset, flags);
	}

	public Task<RedisValue> StringGetRangeAsync(RedisKey key, long start, long end, CommandFlags flags)
	{
		return _inner.StringGetRangeAsync(key, start, end, flags);
	}

	public Task<RedisValue> StringGetSetAsync(RedisKey key, RedisValue value, CommandFlags flags)
	{
		return _inner.StringGetSetAsync(key, value, flags);
	}

	public Task<RedisValue> StringGetSetExpiryAsync(RedisKey key, TimeSpan? expiry, CommandFlags flags)
	{
		return _inner.StringGetSetExpiryAsync(key, expiry, flags);
	}

	public Task<RedisValue> StringGetSetExpiryAsync(RedisKey key, DateTime expiry, CommandFlags flags)
	{
		return _inner.StringGetSetExpiryAsync(key, expiry, flags);
	}

	public Task<RedisValue> StringGetDeleteAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetDeleteAsync(key, flags);
	}

	public Task<RedisValueWithExpiry> StringGetWithExpiryAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringGetWithExpiryAsync(key, flags);
	}

	public Task<long> StringIncrementAsync(RedisKey key, long value, CommandFlags flags)
	{
		return _inner.StringIncrementAsync(key, value, flags);
	}

	public Task<double> StringIncrementAsync(RedisKey key, double value, CommandFlags flags)
	{
		return _inner.StringIncrementAsync(key, value, flags);
	}

	public Task<long> StringLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.StringLengthAsync(key, flags);
	}

	public Task<string> StringLongestCommonSubsequenceAsync(RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequenceAsync(first, second, flags);
	}

	public Task<long> StringLongestCommonSubsequenceLengthAsync(RedisKey first, RedisKey second, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequenceLengthAsync(first, second, flags);
	}

	public Task<LCSMatchResult> StringLongestCommonSubsequenceWithMatchesAsync(RedisKey first, RedisKey second, long minLength, CommandFlags flags)
	{
		return _inner.StringLongestCommonSubsequenceWithMatchesAsync(first, second, minLength, flags);
	}

	public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, When when)
	{
		StringSetCalled = true;
		LastStringSetKey = key.ToString();
		return _inner.StringSetAsync(key, value, expiry, when);
	}

	public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags)
	{
		StringSetCalled = true;
		LastStringSetKey = key.ToString();
		return _inner.StringSetAsync(key, value, expiry, when, flags);
	}

	public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		StringSetCalled = true;
		LastStringSetKey = key.ToString();
		return _inner.StringSetAsync(key, value, expiry, keepTtl, when, flags);
	}

	public Task<bool> StringSetAsync(RedisKey key, RedisValue value, Expiration expiry, ValueCondition when, CommandFlags flags)
	{
		StringSetCalled = true;
		LastStringSetKey = key.ToString();
		return _inner.StringSetAsync(key, value, expiry, when, flags);
	}

	public Task<bool> StringSetAsync(KeyValuePair<RedisKey, RedisValue>[] values, When when, CommandFlags flags)
	{
		return _inner.StringSetAsync(values, when, flags);
	}

	public Task<bool> StringSetAsync(KeyValuePair<RedisKey, RedisValue>[] values, When when, Expiration expiry, CommandFlags flags)
	{
		return _inner.StringSetAsync(values, when, expiry, flags);
	}

	public Task<RedisValue> StringSetAndGetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags)
	{
		return _inner.StringSetAndGetAsync(key, value, expiry, when, flags);
	}

	public Task<RedisValue> StringSetAndGetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
	{
		return _inner.StringSetAndGetAsync(key, value, expiry, keepTtl, when, flags);
	}

	public Task<bool> StringSetBitAsync(RedisKey key, long offset, bool bit, CommandFlags flags)
	{
		return _inner.StringSetBitAsync(key, offset, bit, flags);
	}

	public Task<RedisValue> StringSetRangeAsync(RedisKey key, long offset, RedisValue value, CommandFlags flags)
	{
		return _inner.StringSetRangeAsync(key, offset, value, flags);
	}

	public Task<bool> VectorSetAddAsync(RedisKey key, VectorSetAddRequest request, CommandFlags flags)
	{
		return _inner.VectorSetAddAsync(key, request, flags);
	}

	public Task<long> VectorSetLengthAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetLengthAsync(key, flags);
	}

	public Task<int> VectorSetDimensionAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetDimensionAsync(key, flags);
	}

	public Task<Lease<float>> VectorSetGetApproximateVectorAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetApproximateVectorAsync(key, member, flags);
	}

	public Task<string> VectorSetGetAttributesJsonAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetAttributesJsonAsync(key, member, flags);
	}

	public Task<VectorSetInfo?> VectorSetInfoAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetInfoAsync(key, flags);
	}

	public Task<bool> VectorSetContainsAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetContainsAsync(key, member, flags);
	}

	public Task<Lease<RedisValue>> VectorSetGetLinksAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetLinksAsync(key, member, flags);
	}

	public Task<Lease<VectorSetLink>> VectorSetGetLinksWithScoresAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetGetLinksWithScoresAsync(key, member, flags);
	}

	public Task<RedisValue> VectorSetRandomMemberAsync(RedisKey key, CommandFlags flags)
	{
		return _inner.VectorSetRandomMemberAsync(key, flags);
	}

	public Task<RedisValue[]> VectorSetRandomMembersAsync(RedisKey key, long count, CommandFlags flags)
	{
		return _inner.VectorSetRandomMembersAsync(key, count, flags);
	}

	public Task<bool> VectorSetRemoveAsync(RedisKey key, RedisValue member, CommandFlags flags)
	{
		return _inner.VectorSetRemoveAsync(key, member, flags);
	}

	public Task<bool> VectorSetSetAttributesJsonAsync(RedisKey key, RedisValue member, string attributesJson, CommandFlags flags)
	{
		return _inner.VectorSetSetAttributesJsonAsync(key, member, attributesJson, flags);
	}

	public Task<Lease<VectorSetSimilaritySearchResult>> VectorSetSimilaritySearchAsync(RedisKey key, VectorSetSimilaritySearchRequest query, CommandFlags flags)
	{
		return _inner.VectorSetSimilaritySearchAsync(key, query, flags);
	}

	public Task<Lease<RedisValue>> VectorSetRangeAsync(RedisKey key, RedisValue start, RedisValue end, long count, Exclude exclude, CommandFlags flags)
	{
		return _inner.VectorSetRangeAsync(key, start, end, count, exclude, flags);
	}

	public IAsyncEnumerable<RedisValue> VectorSetRangeEnumerateAsync(RedisKey key, RedisValue start, RedisValue end, long count, Exclude exclude, CommandFlags flags)
	{
		return _inner.VectorSetRangeEnumerateAsync(key, start, end, count, exclude, flags);
	}

	public Task<RedisStream[]> StreamReadGroupAsync(StreamPosition[] streamPositions, RedisValue groupName, RedisValue consumerName, int? countPerStream, bool noAck, TimeSpan? claimMinIdleTime, CommandFlags flags)
	{
		return _inner.StreamReadGroupAsync(streamPositions, groupName, consumerName, countPerStream, noAck, claimMinIdleTime, flags);
	}
}
