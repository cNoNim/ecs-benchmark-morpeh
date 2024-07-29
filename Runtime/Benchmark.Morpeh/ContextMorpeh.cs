using System;
using System.Buffers;
using Benchmark.Core;
using Benchmark.Core.Components;
using Benchmark.Core.Random;
using Scellecs.Morpeh;
using StableHash32 = Benchmark.Core.Hash.StableHash32;

namespace Benchmark.Morpeh
{

public class ContextMorpeh : ContextBase
{
	private World? _world;

	public ContextMorpeh()
		: base("Morpeh") {}

	protected override void DoSetup()
	{
		var world = _world = World.Create();

		var spawnGroup = world.CreateSystemsGroup();
		spawnGroup.AddSystem(new SpawnSystem());
		spawnGroup.AddSystem(new RespawnSystem());
		spawnGroup.AddSystem(new KillSystem());
		world.AddSystemsGroup(0, spawnGroup);

		var updateGroup = world.CreateSystemsGroup();
		updateGroup.AddSystem(new RenderSystem(Framebuffer));
		updateGroup.AddSystem(new SpriteSystem());
		updateGroup.AddSystem(new DamageSystem());
		updateGroup.AddSystem(new AttackSystem());
		updateGroup.AddSystem(new MovementSystem());
		updateGroup.AddSystem(new UpdateVelocitySystem());
		updateGroup.AddSystem(new UpdateDataSystem());
		world.AddSystemsGroup(1, updateGroup);

		var spawnStash = world.GetStash<Tag<Spawn>>();
		var dataStash  = world.GetStash<Comp<Data>>();
		var unitStash  = world.GetStash<Comp<Unit>>();
		for (var i = 0; i < EntityCount; ++i)
		{
			var entity = world.CreateEntity();
			spawnStash.Add(entity);
			dataStash.Add(entity);
			unitStash.Add(
				entity,
				new Unit
				{
					Id   = (uint) i,
					Seed = (uint) i,
				});
		}

		world.Commit();
	}

	protected override void DoRun(int tick) =>
		_world?.Update(0.0f);

	protected override void DoCleanup()
	{
		_world?.Dispose();
		_world = null;
	}

	private class SpawnSystem : ISystem
	{
		private Stash<Comp<Damage>>      _damageStash   = null!;
		private Stash<Comp<Data>>        _dataStash     = null!;
		private Filter                   _filter        = null!;
		private Stash<Comp<Health>>      _healthStash   = null!;
		private Stash<Tag<Unit.Hero>>    _heroStash     = null!;
		private Stash<Tag<Unit.Monster>> _monsterStash  = null!;
		private Stash<Tag<Unit.NPC>>     _npcStash      = null!;
		private Stash<Comp<Position>>    _positionStash = null!;
		private Stash<Tag<Spawn>>        _spawnStash    = null!;
		private Stash<Comp<Sprite>>      _spriteStash   = null!;
		private Stash<Comp<Unit>>        _unitStash     = null!;
		private Stash<Comp<Velocity>>    _velocityStash = null!;
		public  World                    World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Unit>>()
						   .With<Comp<Data>>()
						   .With<Tag<Spawn>>()
						   .Build();
			_unitStash     = World.GetStash<Comp<Unit>>();
			_dataStash     = World.GetStash<Comp<Data>>();
			_spawnStash    = World.GetStash<Tag<Spawn>>();
			_npcStash      = World.GetStash<Tag<Unit.NPC>>();
			_heroStash     = World.GetStash<Tag<Unit.Hero>>();
			_monsterStash  = World.GetStash<Tag<Unit.Monster>>();
			_healthStash   = World.GetStash<Comp<Health>>();
			_damageStash   = World.GetStash<Comp<Damage>>();
			_spriteStash   = World.GetStash<Comp<Sprite>>();
			_positionStash = World.GetStash<Comp<Position>>();
			_velocityStash = World.GetStash<Comp<Velocity>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
			{
				switch (SpawnUnit(
							in _dataStash.Get(entity)
										 .V,
							ref _unitStash.Get(entity)
										  .V,
							out _healthStash.Add(entity)
											.V,
							out _damageStash.Add(entity)
											.V,
							out _spriteStash.Add(entity)
											.V,
							out _positionStash.Add(entity)
											  .V,
							out _velocityStash.Add(entity)
											  .V))
				{
				case UnitType.NPC:
					_npcStash.Add(entity);
					break;
				case UnitType.Hero:
					_heroStash.Add(entity);
					break;
				case UnitType.Monster:
					_monsterStash.Add(entity);
					break;
				}

				_spawnStash.Remove(entity);
			}
		}
	}

	private class UpdateDataSystem : ISystem
	{
		private Stash<Comp<Data>> _dataStash = null!;
		private Filter            _filter    = null!;
		public  World             World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Data>>()
						   .Build();
			_dataStash = World.GetStash<Comp<Data>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
				UpdateDataSystemForEach(
					ref _dataStash.Get(entity)
								  .V);
		}
	}

	private class UpdateVelocitySystem : ISystem
	{
		private Stash<Comp<Data>>     _dataStash     = null!;
		private Filter                _filter        = null!;
		private Stash<Comp<Position>> _positionStash = null!;
		private Stash<Comp<Unit>>     _unitStash     = null!;
		private Stash<Comp<Velocity>> _velocityStash = null!;
		public  World                 World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Velocity>>()
						   .With<Comp<Unit>>()
						   .With<Comp<Data>>()
						   .With<Comp<Position>>()
						   .Without<Tag<Dead>>()
						   .Build();
			_velocityStash = World.GetStash<Comp<Velocity>>();
			_unitStash     = World.GetStash<Comp<Unit>>();
			_dataStash     = World.GetStash<Comp<Data>>();
			_positionStash = World.GetStash<Comp<Position>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
				UpdateVelocitySystemForEach(
					ref _velocityStash.Get(entity)
									  .V,
					ref _unitStash.Get(entity)
								  .V,
					in _dataStash.Get(entity)
								 .V,
					in _positionStash.Get(entity)
									 .V);
		}
	}

	private class MovementSystem : ISystem
	{
		private Filter                _filter        = null!;
		private Stash<Comp<Position>> _positionStash = null!;
		private Stash<Comp<Velocity>> _velocityStash = null!;
		public  World                 World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Position>>()
						   .With<Comp<Velocity>>()
						   .Without<Tag<Dead>>()
						   .Build();
			_positionStash = World.GetStash<Comp<Position>>();
			_velocityStash = World.GetStash<Comp<Velocity>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
				MovementSystemForEach(
					ref _positionStash.Get(entity)
									  .V,
					in _velocityStash.Get(entity)
									 .V);
		}
	}

	private class AttackSystem : ISystem
	{
		private Stash<Comp<Attack<Entity>>> _attackStash   = null!;
		private Stash<Comp<Damage>>         _damageStash   = null!;
		private Stash<Comp<Data>>           _dataStash     = null!;
		private Filter                      _filter        = null!;
		private Stash<Comp<Position>>       _positionStash = null!;
		private Stash<Comp<Unit>>           _unitStash     = null!;
		public  World                       World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Unit>>()
						   .With<Comp<Data>>()
						   .With<Comp<Damage>>()
						   .With<Comp<Position>>()
						   .Without<Tag<Spawn>>()
						   .Without<Tag<Dead>>()
						   .Build();
			_unitStash     = World.GetStash<Comp<Unit>>();
			_dataStash     = World.GetStash<Comp<Data>>();
			_damageStash   = World.GetStash<Comp<Damage>>();
			_positionStash = World.GetStash<Comp<Position>>();
			_attackStash   = World.GetStash<Comp<Attack<Entity>>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			var count   = _filter.GetLengthSlow();
			var keys    = ArrayPool<uint>.Shared.Rent(count);
			var targets = ArrayPool<Target<Entity>>.Shared.Rent(count);
			FillTargets(keys, targets);
			Array.Sort(
				keys,
				targets,
				0,
				count);
			CreateAttacks(targets, count);
			ArrayPool<uint>.Shared.Return(keys);
			ArrayPool<Target<Entity>>.Shared.Return(targets);
		}

		private void FillTargets(uint[] keys, Target<Entity>[] targets)
		{
			var i = 0;
			foreach (var entity in _filter)
			{
				var index = i++;
				keys[index] = _unitStash.Get(entity)
										.V.Id;
				targets[index] = new Target<Entity>(
					entity,
					_positionStash.Get(entity)
								  .V);
			}
		}

		private void CreateAttacks(Target<Entity>[] targets, int count)
		{
			foreach (var entity in _filter)
			{
				ref readonly var damage = ref _damageStash.Get(entity)
														  .V;
				if (damage.Cooldown <= 0)
					continue;

				ref var unit = ref _unitStash.Get(entity)
											 .V;
				ref readonly var data = ref _dataStash.Get(entity)
													  .V;
				var tick = data.Tick - unit.SpawnTick;
				if (tick % damage.Cooldown != 0)
					continue;

				ref readonly var position = ref _positionStash.Get(entity)
															  .V;
				var generator    = new RandomGenerator(unit.Seed);
				var index        = generator.Random(ref unit.Counter, count);
				var target       = targets[index];
				var attackEntity = World.CreateEntity();
				_attackStash.Add(attackEntity) = new Attack<Entity>
				{
					Target = target.Entity,
					Damage = damage.Attack,
					Ticks  = Common.AttackTicks(position.V, target.Position),
				};
			}
		}
	}

	private class DamageSystem : ISystem
	{
		private Filter                      _attackFilter = null!;
		private Stash<Comp<Attack<Entity>>> _attackStash  = null!;
		private Stash<Comp<Damage>>         _damageStash  = null!;
		private Filter                      _filter       = null!;
		private Stash<Comp<Health>>         _healthStash  = null!;
		public  World                       World { get; set; } = null!;

		public void OnAwake()
		{
			_attackFilter = World.Filter.With<Comp<Attack<Entity>>>()
								 .Build();
			_filter = World.Filter.With<Comp<Health>>()
						   .With<Comp<Damage>>()
						   .Without<Tag<Dead>>()
						   .Build();
			_attackStash = World.GetStash<Comp<Attack<Entity>>>();
			_healthStash = World.GetStash<Comp<Health>>();
			_damageStash = World.GetStash<Comp<Damage>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _attackFilter)
			{
				ref var attack = ref _attackStash.Get(entity)
												 .V;
				if (attack.Ticks-- > 0)
					continue;

				var target       = attack.Target;
				var attackDamage = attack.Damage;

				World.RemoveEntity(entity);

				if (!_filter.Has(target))
					continue;

				ref var health = ref _healthStash.Get(target)
												 .V;
				ref readonly var damage = ref _damageStash.Get(target)
														  .V;
				var totalDamage = attackDamage - damage.Defence;
				health.Hp -= totalDamage;
			}
		}
	}

	private class KillSystem : ISystem
	{
		private Stash<Comp<Data>>   _dataStash   = null!;
		private Stash<Tag<Dead>>    _deadStash   = null!;
		private Filter              _filter      = null!;
		private Stash<Comp<Health>> _healthStash = null!;
		private Stash<Comp<Unit>>   _unitStash   = null!;
		public  World               World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Unit>>()
						   .With<Comp<Health>>()
						   .With<Comp<Data>>()
						   .Without<Tag<Dead>>()
						   .Build();
			_healthStash = World.GetStash<Comp<Health>>();
			_unitStash   = World.GetStash<Comp<Unit>>();
			_dataStash   = World.GetStash<Comp<Data>>();
			_deadStash   = World.GetStash<Tag<Dead>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
			{
				ref readonly var health = ref _healthStash.Get(entity)
														  .V;
				if (health.Hp > 0)
					continue;

				ref var unit = ref _unitStash.Get(entity)
											 .V;
				_deadStash.Add(entity);
				ref readonly var data = ref _dataStash.Get(entity)
													  .V;
				unit.RespawnTick = data.Tick + RespawnTicks;
			}
		}
	}

	private class SpriteSystem : ISystem
	{
		private Filter              _deadFilter    = null!;
		private Filter              _heroFilter    = null!;
		private Filter              _monsterFilter = null!;
		private Filter              _npcFilter     = null!;
		private Filter              _spawnFilter   = null!;
		private Stash<Comp<Sprite>> _spriteStash   = null!;
		public  World               World { get; set; } = null!;

		public void OnAwake()
		{
			_spawnFilter = World.Filter.With<Comp<Sprite>>()
								.With<Tag<Spawn>>()
								.Build();
			_deadFilter = World.Filter.With<Comp<Sprite>>()
							   .With<Tag<Dead>>()
							   .Build();
			_npcFilter = World.Filter.With<Comp<Sprite>>()
							  .With<Tag<Unit.NPC>>()
							  .Without<Tag<Spawn>>()
							  .Without<Tag<Dead>>()
							  .Build();
			_heroFilter = World.Filter.With<Comp<Sprite>>()
							   .With<Tag<Unit.Hero>>()
							   .Without<Tag<Spawn>>()
							   .Without<Tag<Dead>>()
							   .Build();
			_monsterFilter = World.Filter.With<Comp<Sprite>>()
								  .With<Tag<Unit.Monster>>()
								  .Without<Tag<Spawn>>()
								  .Without<Tag<Dead>>()
								  .Build();
			_spriteStash = World.GetStash<Comp<Sprite>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			ForEachSprite(_spawnFilter,   SpriteMask.Spawn);
			ForEachSprite(_deadFilter,    SpriteMask.Grave);
			ForEachSprite(_npcFilter,     SpriteMask.NPC);
			ForEachSprite(_heroFilter,    SpriteMask.Hero);
			ForEachSprite(_monsterFilter, SpriteMask.Monster);
		}

		private void ForEachSprite(Filter filter, SpriteMask sprite)
		{
			foreach (var entity in filter)
				_spriteStash.Get(entity)
							.V.Character = sprite;
		}
	}

	private class RenderSystem : ISystem
	{
		private readonly Framebuffer           _framebuffer;
		private          Stash<Comp<Data>>     _datas     = null!;
		private          Filter                _filter    = null!;
		private          Stash<Comp<Position>> _positions = null!;
		private          Stash<Comp<Sprite>>   _sprites   = null!;
		private          Stash<Comp<Unit>>     _units     = null!;

		public RenderSystem(Framebuffer framebuffer) =>
			_framebuffer = framebuffer;

		public World World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Position>>()
						   .With<Comp<Sprite>>()
						   .With<Comp<Data>>()
						   .Build();
			_positions = World.GetStash<Comp<Position>>();
			_sprites   = World.GetStash<Comp<Sprite>>();
			_units     = World.GetStash<Comp<Unit>>();
			_datas     = World.GetStash<Comp<Data>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
				RenderSystemForEach(
					_framebuffer,
					in _positions.Get(entity)
								 .V,
					in _sprites.Get(entity)
							   .V,
					in _units.Get(entity)
							 .V,
					in _datas.Get(entity)
							 .V);
		}
	}

	private class RespawnSystem : ISystem
	{
		private Stash<Comp<Data>> _dataStash  = null!;
		private Filter            _filter     = null!;
		private Stash<Tag<Spawn>> _spawnStash = null!;
		private Stash<Comp<Unit>> _unitStash  = null!;
		public  World             World { get; set; } = null!;

		public void OnAwake()
		{
			_filter = World.Filter.With<Comp<Unit>>()
						   .With<Comp<Data>>()
						   .With<Tag<Dead>>()
						   .Build();
			_spawnStash = World.GetStash<Tag<Spawn>>();
			_dataStash  = World.GetStash<Comp<Data>>();
			_unitStash  = World.GetStash<Comp<Unit>>();
		}

		public void Dispose() {}

		public void OnUpdate(float deltaTime)
		{
			foreach (var entity in _filter)
			{
				ref readonly var unit = ref _unitStash.Get(entity)
													  .V;
				ref readonly var data = ref _dataStash.Get(entity)
													  .V;

				if (data.Tick < unit.RespawnTick)
					continue;

				var newEntity = World.CreateEntity();
				_spawnStash.Add(newEntity);
				_dataStash.Add(newEntity) = data;
				_unitStash.Add(newEntity) = new Unit
				{
					Id   = unit.Id | (uint) data.Tick << 16,
					Seed = StableHash32.Hash(unit.Seed, unit.Counter),
				};
				World.RemoveEntity(entity);
			}
		}
	}
}

}
