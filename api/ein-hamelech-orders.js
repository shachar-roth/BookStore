const MAX_FIELD_LENGTH = 500;
const ALLOWED_ORIGIN = "https://ein-hamelech.shakedshira.com";
const WORKER_VERSION = "2026-06-05-order-options-1";

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: corsHeaders()
      });
    }

    if (request.method !== "POST") {
      return json({ error: "METHOD_NOT_ALLOWED" }, 405);
    }

    try {
      const order = await readOrder(request);
      normalizeOrder(order);
      const validationError = validateOrder(order);

      if (validationError) {
        return json({ error: validationError }, 400);
      }

      const orderRef = createOrderReference();
      await sendOrderEmail(env, order, orderRef);

      return json({ orderRef });
    } catch (error) {
      console.error(`Order submission failed (${WORKER_VERSION})`, error);
      return json({ error: "ORDER_FAILED" }, 500);
    }
  }
};

async function readOrder(request) {
  const contentType = request.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    return await request.json();
  }

  const formData = await request.formData();
  return Object.fromEntries(formData.entries());
}

function normalizeOrder(order) {
  order.bookType = order.bookType === "digital" ? "digital" : "physical";
  order.deliveryMethod =
    order.bookType === "digital"
      ? "digital"
      : order.deliveryMethod === "home-delivery"
        ? "home-delivery"
        : "israel-mail";
  order.unitPrice = order.bookType === "digital" ? "27" : "60";
}

function validateOrder(order) {
  const requiredFields = ["name", "phone", "email", "quantity"];

  if (order.bookType === "physical") {
    requiredFields.push("address", "city");
  }

  for (const field of requiredFields) {
    if (!isNonEmptyString(order[field])) {
      return "MISSING_FIELDS";
    }
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(order.email)) {
    return "INVALID_EMAIL";
  }

  const quantity = Number(order.quantity);
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > 25) {
    return "INVALID_QUANTITY";
  }

  for (const value of Object.values(order)) {
    if (typeof value === "string" && value.length > MAX_FIELD_LENGTH) {
      return "FIELD_TOO_LONG";
    }
  }

  return null;
}

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function createOrderReference() {
  const now = new Date();
  const israelParts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Jerusalem",
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).formatToParts(now);
  const values = Object.fromEntries(israelParts.map((part) => [part.type, part.value]));
  const daySeed = Number(`${values.year}${values.month}${values.day}`);
  const secondsSeed = Math.floor(now.getTime() / 1000);
  const randomSeed = Math.floor(Math.random() * 1296);
  const code = ((secondsSeed + daySeed + randomSeed) % 1679616)
    .toString(36)
    .toUpperCase()
    .padStart(4, "0");

  return `EM-${code}`;
}

async function sendOrderEmail(env, order, orderRef) {
  const apiKey = await getSecretValue(env.RESEND_API_KEY);
  const to = env.ORDER_EMAIL_TO;
  const from = env.ORDER_EMAIL_FROM;

  if (!apiKey || !to || !from) {
    throw new Error("Missing order email configuration");
  }

  const emailPayload = {
    from,
    to: [to],
    subject: `הזמנה חדשה - ${orderRef}`,
    text: createEmailText(order, orderRef)
  };

  const response = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json"
    },
    body: JSON.stringify(emailPayload)
  });

  if (!response.ok) {
    const errorText = await response.text();
    console.error("Resend rejected order email", {
      workerVersion: WORKER_VERSION,
      status: response.status,
      response: errorText,
      from,
      to
    });
    throw new Error(`Resend failed with status ${response.status}: ${errorText}`);
  }
}

function createEmailText(order, orderRef) {
  const quantity = Number(order.quantity);
  const unitPrice = Number(order.unitPrice);
  const total = quantity * unitPrice;
  const bookTypeText = order.bookType === "digital" ? "ספר דיגיטלי" : "ספר פיזי";
  const deliveryText =
    order.bookType === "digital"
      ? "שליחה במייל"
      : order.deliveryMethod === "home-delivery"
        ? "משלוח עד הבית - עלות משלוח תתואם בהמשך"
        : "דואר ישראל - חינם";

  return [
    `מספר הזמנה: ${orderRef}`,
    "",
    "פרטי לקוח:",
    `שם: ${clean(order.name)}`,
    `טלפון: ${clean(order.phone)}`,
    `אימייל: ${clean(order.email)}`,
    "",
    "פרטי הזמנה:",
    `סוג ספר: ${bookTypeText}`,
    `אפשרות קבלה: ${deliveryText}`,
    `כמות: ${quantity}`,
    `מחיר ליחידה: ${unitPrice} ש"ח`,
    `סה"כ לתשלום לפני משלוח עד הבית: ${total} ש"ח`,
    "",
    "פרטי משלוח:",
    `כתובת: ${clean(order.address) || "-"}`,
    `עיר: ${clean(order.city) || "-"}`,
    `מיקוד: ${clean(order.postalCode) || "-"}`,
    "",
    `הערות: ${clean(order.notes) || "-"}`,
    "",
    "יש להתאים את ההזמנה לתשלום בביט/פייבוקס לפי מספר ההזמנה והשם."
  ].join("\n");
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}

async function getSecretValue(secret) {
  if (secret && typeof secret.get === "function") {
    return await secret.get();
  }

  return secret;
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      ...corsHeaders()
    }
  });
}

function corsHeaders() {
  return {
    "Access-Control-Allow-Origin": ALLOWED_ORIGIN,
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type"
  };
}
