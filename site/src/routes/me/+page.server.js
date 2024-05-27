import { redirect } from '@sveltejs/kit';

export const load = async (event) => {
	// get the sessionId from the cookie
	const sessionId = event.cookies.get('sessionId');

	// if there is no sessionId, redirect to the sign-in page
	if (!sessionId) {
		throw redirect(301, '/sign-in');
	}

	// get the user's info from the server
	const res = await event.fetch(`https://api.just-a-frogger.ru/users/me`, {
		headers: { Authorization: `Bearer ${sessionId}` }
	});

	// check the status
	if (res.ok) {
		const user = await res.json();

		return {
			props: {
				user
			}
		};
	}
};
