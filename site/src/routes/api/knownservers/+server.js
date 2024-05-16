export const GET = async () => {
	return new Response(
		JSON.stringify([
			{
				id: 1,
				name: 'Test Server 1',
				description:
					'Lorem ipsum dolor sit amet consectetur adipisicing elit. Numquam aspernatur provident eveniet eligendi cumque consequatur tempore  sint nisi sapiente. Iste beatae laboriosam iure molestias cum expedita architecto itaque quae rem.',
				status: 'Offline',
				url: '127.0.0.1:8080'
			},
			{
				id: 2,
				name: 'Seven Gods Caves',
				description:
					'Добро пожаловать на сервер Seven Gods Caves - увлекательный мир викингов, где каждый игрок может стать героем своей собственной саги. Исследуйте древние пещеры, сражайтесь с могущественными боссами и собирайте древние реликвии, чтобы заслужить благословение семи богов. Присоединяйтесь к кланам, стройте свои поселения и сразитесь за власть в этом жестоком, но волнующем мире викингов. Подготовьтесь к захватывающим приключениям и эпическим сражениям на сервере Seven Gods Caves! 🛡️⚔️🔥',
				status: 'Offline',
				url: '127.0.0.1:8080'
			}
		]),
		{ status: 200 }
	);
};
