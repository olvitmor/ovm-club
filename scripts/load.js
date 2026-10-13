// Нагрузочная проверка (docs/adr/170_load-testing.md).
// Запуск — в docs/adr/170_load-testing.md, раздел «Прогон».
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://host.docker.internal:5080';
const PASSWORD = 'Password123!';
const data = JSON.parse(open(__ENV.DATA || '/data/load-data.json'));
const popularClassId = data.popularClassIds[Number(__ENV.POPULAR || 0)];

http.setResponseCallback(http.expectedStatuses(200, 201, 409));

const baseScenarios = {
    enroll_popular: { executor: 'shared-iterations', vus: 50, iterations: 100, maxDuration: '30s', exec: 'enrollPopular' },
    schedule: { executor: 'constant-vus', vus: 20, duration: '30s', exec: 'schedule' },
    analytics: { executor: 'constant-vus', vus: 2, duration: '30s', exec: 'analytics' },
    card: { executor: 'constant-vus', vus: 20, duration: '30s', exec: 'card' },
};

// Поиск предела: STRESS=1 — чтение расписания и карточек с плавным ростом числа VU.
// Предел — ступень, после которой http_reqs/s перестаёт расти, а p95 резко растёт.
const stressScenarios = {
    stress: {
        executor: 'ramping-vus',
        startVUs: 0,
        stages: [
            { duration: '30s', target: 100 },
            { duration: '30s', target: 300 },
            { duration: '30s', target: 500 },
            { duration: '30s', target: 500 },
            { duration: '15s', target: 0 },
        ],
        gracefulRampDown: '10s',
        exec: 'mixed',
    },
};

const stress = __ENV.STRESS === '1';

export const options = {
    scenarios: stress ? stressScenarios : baseScenarios,
    thresholds: stress
        ? { http_req_failed: ['rate<0.01'] }
        : {
            http_req_failed: ['rate<0.01'],
            'http_req_duration{scenario:enroll_popular}': ['p(95)<500'],
            'http_req_duration{scenario:schedule}': ['p(95)<500'],
            'http_req_duration{scenario:analytics}': ['p(95)<2000'],
            'http_req_duration{scenario:card}': ['p(95)<200'],
        },
    summaryTrendStats: ['avg', 'p(50)', 'p(95)', 'p(99)', 'max'],
};

function login(email) {
    const response = http.post(`${BASE}/auth/login`, JSON.stringify({ email, password: PASSWORD }), {
        headers: { 'Content-Type': 'application/json' },
    });

    return response.json('accessToken');
}

export function setup() {
    return {
        users: data.users.map((user) => ({ token: login(user.email), enrollmentId: user.enrollmentId })),
        adminToken: login(data.admin),
    };
}

function auth(token, extra = {}) {
    return { headers: Object.assign({ Authorization: `Bearer ${token}` }, extra) };
}

function userOf(state) {
    return state.users[(__VU + __ITER) % state.users.length];
}

export function enrollPopular(state) {
    const user = state.users[__ITER % state.users.length];
    const response = http.post(
        `${BASE}/enrollments`,
        JSON.stringify({ classId: popularClassId, seats: 1 }),
        auth(user.token, { 'Content-Type': 'application/json', 'Idempotency-Key': `${__VU}-${__ITER}-${Date.now()}` }));

    check(response, { 'enroll 201 or 409': (r) => r.status === 201 || r.status === 409 });
}

export function schedule(state) {
    const from = new Date();
    const to = new Date(from.getTime() + 7 * 24 * 3600 * 1000);
    let token = null;

    for (let page = 0; page < 3; page++) {
        const query = `from=${from.toISOString()}&to=${to.toISOString()}&limit=100`
            + (token ? `&continuationToken=${encodeURIComponent(token)}` : '');
        const response = http.get(`${BASE}/schedule?${query}`, auth(userOf(state).token));

        check(response, { 'schedule 200': (r) => r.status === 200 });
        token = response.json('continuationToken');

        if (!token) {
            break;
        }
    }
}

export function analytics(state) {
    const to = new Date();
    const from = new Date(to.getTime() - 30 * 24 * 3600 * 1000);
    const query = `from=${from.toISOString().slice(0, 10)}&to=${to.toISOString().slice(0, 10)}`;
    const response = http.get(`${BASE}/analytics/load?${query}`, auth(state.adminToken));

    check(response, { 'analytics 200': (r) => r.status === 200 });
}

export function card(state) {
    const user = userOf(state);
    const response = http.get(`${BASE}/enrollments/${user.enrollmentId}`, auth(user.token));

    check(response, { 'card 200': (r) => r.status === 200 });
}

export function mixed(state) {
    if (Math.random() < 0.5) {
        schedule(state);
    } else {
        card(state);
    }
}
