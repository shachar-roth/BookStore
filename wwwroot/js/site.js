const hero = document.querySelector("[data-video-hero]");
const heroVideo = document.querySelector("[data-hero-video]");

if (hero && heroVideo) {
  const observer = new IntersectionObserver(
    (entries) => {
      const [entry] = entries;

      if (!entry.isIntersecting || entry.intersectionRatio < 0.6) {
        heroVideo.pause();
      }
    },
    {
      threshold: [0, 0.6, 1]
    }
  );

  observer.observe(hero);
}

const orderForm = document.querySelector("[data-order-form]");
const orderDetails = document.querySelector("[data-order-details]");

if (orderDetails) {
  const params = new URLSearchParams(window.location.search);
  const bookType = params.get("bookType") === "digital" ? "digital" : "physical";
  const deliveryMethod =
    bookType === "digital"
      ? "digital"
      : params.get("deliveryMethod") === "home-delivery"
        ? "home-delivery"
        : "israel-mail";
  const unitPrice = bookType === "digital" ? "27" : "60";
  const optionTitle =
    bookType === "digital"
      ? "ספר דיגיטלי"
      : deliveryMethod === "home-delivery"
        ? "ספר פיזי - משלוח עד הבית"
        : "ספר פיזי - דואר ישראל";
  const deliveryText =
    bookType === "digital"
      ? "הספר הדיגיטלי יישלח למייל לאחר אישור התשלום."
      : deliveryMethod === "home-delivery"
        ? "עלות המשלוח עד הבית עדיין לא ידועה ותתואם בהמשך."
        : "משלוח בדואר ישראל ללא עלות נוספת.";

  const optionTitleElement = orderDetails.querySelector("[data-order-option-title]");
  const unitPriceElement = orderDetails.querySelector("[data-order-unit-price]");
  const deliveryTextElement = orderDetails.querySelector("[data-order-delivery-text]");
  const bookTypeInput = orderDetails.querySelector("[data-order-book-type]");
  const deliveryMethodInput = orderDetails.querySelector("[data-order-delivery-method]");
  const unitPriceInput = orderDetails.querySelector("[data-order-unit-price-input]");
  const shippingFields = orderDetails.querySelector("[data-order-shipping-fields]");

  if (optionTitleElement) optionTitleElement.textContent = optionTitle;
  if (unitPriceElement) unitPriceElement.textContent = unitPrice;
  if (deliveryTextElement) deliveryTextElement.textContent = deliveryText;
  if (bookTypeInput) bookTypeInput.value = bookType;
  if (deliveryMethodInput) deliveryMethodInput.value = deliveryMethod;
  if (unitPriceInput) unitPriceInput.value = unitPrice;

  if (shippingFields && bookType === "digital") {
    shippingFields.hidden = true;
    shippingFields.querySelectorAll("[data-shipping-required]").forEach((input) => {
      input.required = false;
    });
  }
}

if (orderForm) {
  const submitButton = orderForm.querySelector("[data-order-submit]");
  const status = orderForm.querySelector("[data-order-status]");

  orderForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (status) {
      status.textContent = "שולחים את ההזמנה...";
    }

    if (submitButton) {
      submitButton.disabled = true;
    }

    try {
      const formData = new FormData(orderForm);
      const payload = Object.fromEntries(formData.entries());

      const response = await fetch("/api/order", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json().catch(() => ({}));

      if (!response.ok || !result.orderRef) {
        throw new Error(result.error || "ORDER_FAILED");
      }

      const thanksUrl = orderForm.dataset.thanksUrl || "/OrderThanks";
      const redirectParams = new URLSearchParams({
        orderRef: result.orderRef,
        bookType: payload.bookType || "physical",
        deliveryMethod: payload.deliveryMethod || "israel-mail",
        quantity: payload.quantity || "1",
        unitPrice: payload.unitPrice || "60"
      });
      window.location.href = `${thanksUrl}?${redirectParams.toString()}`;
    } catch {
      if (status) {
        status.textContent = "לא הצלחנו לשלוח את ההזמנה. נסו שוב בעוד רגע או צרו קשר בטלפון.";
      }

      if (submitButton) {
        submitButton.disabled = false;
      }
    }
  });
}

const orderReference = document.querySelector("[data-order-reference]");
const inlineOrderReference = document.querySelector("[data-order-reference-inline]");
const orderPaymentSummary = document.querySelector("[data-order-payment-summary]");

if (orderReference || inlineOrderReference || orderPaymentSummary) {
  const params = new URLSearchParams(window.location.search);
  const orderRef = params.get("orderRef");
  const bookType = params.get("bookType") || "physical";
  const deliveryMethod = params.get("deliveryMethod") || "israel-mail";
  const quantity = Number(params.get("quantity") || "1");
  const unitPrice = Number(params.get("unitPrice") || (bookType === "digital" ? "27" : "60"));

  if (orderRef) {
    if (orderReference) {
      orderReference.textContent = orderRef;
    }

    if (inlineOrderReference) {
      inlineOrderReference.textContent = orderRef;
    }
  }

  if (orderPaymentSummary && Number.isFinite(quantity) && Number.isFinite(unitPrice)) {
    const total = quantity * unitPrice;
    const typeText = bookType === "digital" ? "ספר דיגיטלי" : "ספר פיזי";
    const deliveryText =
      bookType === "digital"
        ? "שליחה במייל"
        : deliveryMethod === "home-delivery"
          ? "משלוח עד הבית, עלות משלוח תתואם בהמשך"
          : "דואר ישראל, משלוח חינם";

    orderPaymentSummary.textContent = `סיכום לתשלום: ${typeText}, כמות ${quantity}, ${total} ש\"ח. ${deliveryText}.`;
  }
}
